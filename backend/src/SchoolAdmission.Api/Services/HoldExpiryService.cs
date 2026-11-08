using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Services;

/// <summary>
/// Periodically marks lapsed holds as EXPIRED so the stored status matches reality.
/// Seat counts do NOT depend on this service: the count queries already ignore HELD rows whose
/// expiresAt has passed, so seats return to the pool at the exact moment a hold lapses.
/// Safe to run in several backend processes at once (it is one idempotent UPDATE).
/// </summary>
public class HoldExpiryService(
    IServiceScopeFactory scopes,
    IOptions<HoldOptions> options,
    TimeProvider time,
    ILogger<HoldExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds);
        using var timer = new PeriodicTimer(interval, time);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let a transient DB error kill the sweeper.
                logger.LogWarning(ex, "Expiry sweep failed; will retry in {Interval}s.", interval.TotalSeconds);
            }
        } while (await WaitAsync(timer, stoppingToken));
    }

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var now = time.GetUtcNow().UtcDateTime;

        var expired = await db.Holds
            .Where(h => h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.EXPIRED), ct);

        if (expired > 0)
            logger.LogInformation("Marked {Count} hold(s) as EXPIRED.", expired);
        return expired;
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
