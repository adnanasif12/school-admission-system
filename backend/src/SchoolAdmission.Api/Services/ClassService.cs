using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Api;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Services;

public class ClassService(AdmissionDbContext db, TimeProvider time)
{
    public const int MaxSeats = 1_000_000;

    public static string NewId(string prefix) =>
        prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

    public async Task<ClassCreatedResponse> CreateAsync(CreateClassRequest req, CancellationToken ct)
    {
        var name = req.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new ApiException(400, "INVALID_NAME", "Please enter a class name, for example \"Class 6 - Morning Shift\".");
        if (name.Length > 200)
            throw new ApiException(400, "INVALID_NAME", "The class name is too long (200 characters at most).");
        if (req.TotalSeats is null or < 1 or > MaxSeats)
            throw new ApiException(400, "INVALID_SEATS", $"Total seats must be a whole number between 1 and {MaxSeats:N0}.");

        var entity = new SchoolClass
        {
            Id = NewId("c_"),
            Name = name,
            TotalSeats = req.TotalSeats.Value,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Classes.Add(entity);
        await db.SaveChangesAsync(ct);
        return new ClassCreatedResponse(entity.Id, entity.Name, entity.TotalSeats);
    }

    public async Task<List<ClassResponse>> ListAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var rows = await Project(db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt).ThenBy(c => c.Id), now).ToListAsync(ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<ClassResponse> GetAsync(string classId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var row = await Project(db.Classes.AsNoTracking().Where(c => c.Id == classId), now).FirstOrDefaultAsync(ct)
                  ?? throw new ApiException(404, "CLASS_NOT_FOUND", "We could not find that class.");
        return ToResponse(row);
    }

    // A HELD row only counts while expiresAt is still in the future, so seats come back the instant a
    // hold lapses, even before the background sweeper has flipped the row to EXPIRED.
    // Filter/order BEFORE projecting: EF cannot translate OrderBy/Where over the projected record.
    private IQueryable<ClassCounts> Project(IQueryable<SchoolClass> source, DateTime now) => source.Select(c => new ClassCounts(
        c.Id, c.Name, c.TotalSeats, c.CreatedAt,
        db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.HELD && h.ExpiresAt > now),
        db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.CONFIRMED)));

    private static ClassResponse ToResponse(ClassCounts c) =>
        new(c.Id, c.Name, c.TotalSeats, c.Held, c.Confirmed, Math.Max(0, c.TotalSeats - c.Held - c.Confirmed));

    private record ClassCounts(string Id, string Name, int TotalSeats, DateTime CreatedAt, int Held, int Confirmed);
}
