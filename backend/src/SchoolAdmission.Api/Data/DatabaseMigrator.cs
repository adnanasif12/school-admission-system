using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Applies EF Core migrations at startup. Waits for PostgreSQL to accept connections, and takes a
/// Postgres advisory lock so two backend processes starting together do not migrate concurrently.
/// </summary>
public static class DatabaseMigrator
{
    private const long MigrationLockKey = 7_272_001;

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
                await conn.OpenAsync(ct);

                await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(@k)", conn))
                {
                    lockCmd.Parameters.AddWithValue("k", MigrationLockKey);
                    await lockCmd.ExecuteScalarAsync(ct);
                }

                try
                {
                    await db.Database.MigrateAsync(ct);
                }
                finally
                {
                    await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(@k)", conn);
                    unlock.Parameters.AddWithValue("k", MigrationLockKey);
                    await unlock.ExecuteScalarAsync(CancellationToken.None);
                }

                logger.LogInformation("Database is up to date.");
                return;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/30): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
