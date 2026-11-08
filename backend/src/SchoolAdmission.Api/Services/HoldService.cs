using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolAdmission.Api.Api;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Services;

public class HoldService(AdmissionDbContext db, IOptions<HoldOptions> options, TimeProvider time)
{
    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- create

    /// <summary>
    /// Reserves a seat. The whole decision runs in one transaction that first takes a row lock on the
    /// class (SELECT ... FOR UPDATE). Every hold/confirm/cancel for that class therefore queues up on
    /// the lock, so "count, then insert" can never interleave across requests or backend processes.
    /// </summary>
    public async Task<HoldCreatedResponse> CreateAsync(string classId, string studentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var totalSeats = await LockClassAsync(classId, ct)
                         ?? throw new ApiException(404, "CLASS_NOT_FOUND", "We could not find that class.");

        // Taken after the lock is acquired so the clock is fresh even if we waited in the queue.
        var now = Now();

        // Retire lapsed holds of this class first. Otherwise a stale HELD row of the same student
        // would trip the partial unique index although the seat is already free again.
        await db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.EXPIRED), ct);

        var alreadyActive = await db.Holds.AnyAsync(h =>
            h.ClassId == classId && h.StudentId == studentId &&
            (h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED), ct);
        if (alreadyActive)
            throw new ApiException(409, "ALREADY_APPLIED",
                "You already have an application for this class. Check \"My applications\" to pay the fee or cancel it.");

        var held = await db.Holds.CountAsync(h =>
            h.ClassId == classId && h.Status == HoldStatus.HELD && h.ExpiresAt > now, ct);
        var confirmed = await db.Holds.CountAsync(h =>
            h.ClassId == classId && h.Status == HoldStatus.CONFIRMED, ct);
        if (held + confirmed >= totalSeats)
            throw new ApiException(409, "NO_SEATS", "Sorry, there are no seats left in this class.");

        var hold = new Hold
        {
            Id = ClassService.NewId("h_"),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.HELD,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(options.Value.DurationSeconds),
        };
        db.Holds.Add(hold);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Safety net: the partial unique index caught a duplicate the checks above missed.
            throw new ApiException(409, "ALREADY_APPLIED", "You already have an application for this class.");
        }

        await tx.CommitAsync(ct);
        return new HoldCreatedResponse(hold.Id, hold.ClassId, hold.StudentId, nameof(HoldStatus.HELD),
            RequestHelpers.Iso(hold.ExpiresAt));
    }

    // ------------------------------------------------------------------ read

    /// <param name="requesterId">When the caller sent X-Student-Id it must own the hold.</param>
    public async Task<HoldResponse> GetAsync(string holdId, string? requesterId, CancellationToken ct)
    {
        var row = await db.Holds.AsNoTracking()
                      .Where(h => h.Id == holdId)
                      .Select(h => new { h.Id, h.ClassId, ClassName = h.Class!.Name, h.StudentId, h.Status, h.ExpiresAt })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new ApiException(404, "HOLD_NOT_FOUND", "We could not find that application.");

        if (requesterId is not null && requesterId != row.StudentId)
            throw new ApiException(403, "NOT_YOUR_HOLD", "This application belongs to a different student.");

        return new HoldResponse(row.Id, row.ClassId, row.ClassName, row.StudentId,
            Effective(row.Status, row.ExpiresAt, Now()).ToString(), RequestHelpers.Iso(row.ExpiresAt));
    }

    public async Task<List<MyHoldResponse>> ListMineAsync(string studentId, CancellationToken ct)
    {
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt).ThenBy(h => h.Id)
            .Select(h => new { h.Id, h.ClassId, ClassName = h.Class!.Name, h.Status, h.ExpiresAt })
            .ToListAsync(ct);

        var now = Now();
        return rows.Select(r => new MyHoldResponse(r.Id, r.ClassId, r.ClassName,
            Effective(r.Status, r.ExpiresAt, now).ToString(), RequestHelpers.Iso(r.ExpiresAt))).ToList();
    }

    // --------------------------------------------------------------- confirm

    /// <summary>"Pay fee": HELD -> CONFIRMED. Idempotent: confirming an already confirmed hold is a no-op 200.</summary>
    public async Task<ConfirmResponse> ConfirmAsync(string holdId, string studentId, CancellationToken ct)
    {
        var hold = await LoadOwnedAsync(holdId, studentId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClassAsync(hold.ClassId, ct);
        var now = Now();

        // Single conditional UPDATE: only a HELD row that has not yet expired can flip. It stays correct
        // even against the background sweeper because the two touch disjoint rows (expiresAt > now vs <= now).
        var flipped = await db.Holds
            .Where(h => h.Id == holdId && h.Status == HoldStatus.HELD && h.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HoldStatus.CONFIRMED)
                .SetProperty(h => h.ConfirmedAt, now), ct);

        if (flipped == 1)
        {
            await tx.CommitAsync(ct);
            return new ConfirmResponse(holdId, nameof(HoldStatus.CONFIRMED));
        }

        var current = await db.Holds.AsNoTracking().FirstAsync(h => h.Id == holdId, ct);
        switch (current.Status)
        {
            case HoldStatus.CONFIRMED:
                return new ConfirmResponse(holdId, nameof(HoldStatus.CONFIRMED)); // idempotent repeat
            case HoldStatus.CANCELLED:
                throw new ApiException(409, "HOLD_CANCELLED", "This application was cancelled, so it cannot be paid. Please apply again.");
            default:
                if (current.Status == HoldStatus.HELD)
                {
                    await db.Holds.Where(h => h.Id == holdId && h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
                        .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.EXPIRED), ct);
                    await tx.CommitAsync(ct);
                }
                throw new ApiException(409, "HOLD_EXPIRED", "Time is up: this application has expired and the seat was released. Please apply again.");
        }
    }

    // ---------------------------------------------------------------- cancel

    /// <summary>Cancels an application and frees its seat. Repeating it is harmless (204).</summary>
    public async Task CancelAsync(string holdId, string studentId, CancellationToken ct)
    {
        var hold = await LoadOwnedAsync(holdId, studentId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClassAsync(hold.ClassId, ct);
        var now = Now();

        var cancelled = await db.Holds
            .Where(h => h.Id == holdId && h.Status == HoldStatus.HELD && h.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.CANCELLED), ct);

        if (cancelled == 0)
        {
            var current = await db.Holds.AsNoTracking().FirstAsync(h => h.Id == holdId, ct);
            if (current.Status == HoldStatus.CONFIRMED)
                throw new ApiException(409, "ALREADY_CONFIRMED",
                    "The admission fee is already paid, so this application can no longer be cancelled here.");

            // Already CANCELLED/EXPIRED, or HELD but lapsed: nothing to free. Record the lapse.
            await db.Holds.Where(h => h.Id == holdId && h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.EXPIRED), ct);
        }

        await tx.CommitAsync(ct);
    }

    // --------------------------------------------------------------- helpers

    /// <summary>A HELD hold past its expiry is reported as EXPIRED even before the sweeper has stored that.</summary>
    private static HoldStatus Effective(HoldStatus stored, DateTime expiresAt, DateTime now) =>
        stored == HoldStatus.HELD && expiresAt <= now ? HoldStatus.EXPIRED : stored;

    private async Task<Hold> LoadOwnedAsync(string holdId, string studentId, CancellationToken ct)
    {
        var hold = await db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId, ct)
                   ?? throw new ApiException(404, "HOLD_NOT_FOUND", "We could not find that application.");
        if (hold.StudentId != studentId)
            throw new ApiException(403, "NOT_YOUR_HOLD", "This application belongs to a different student.");
        return hold;
    }

    /// <summary>SELECT ... FOR UPDATE on the class row inside the current transaction. Null if no such class.</summary>
    private async Task<int?> LockClassAsync(string classId, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = "SELECT \"TotalSeats\" FROM classes WHERE \"Id\" = @id FOR UPDATE";
        var p = cmd.CreateParameter();
        p.ParameterName = "id";
        p.Value = classId;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int seats ? seats : null;
    }
}
