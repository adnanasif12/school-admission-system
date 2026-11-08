namespace SchoolAdmission.Api.Services;

public class HoldOptions
{
    public const int DefaultDurationSeconds = 600;

    /// <summary>HOLD_DURATION_SECONDS: how long a hold keeps a seat before it is released.</summary>
    public int DurationSeconds { get; set; } = DefaultDurationSeconds;

    /// <summary>EXPIRY_SWEEP_INTERVAL_SECONDS: how often the background service marks stale holds EXPIRED.</summary>
    public int SweepIntervalSeconds { get; set; } = 5;

    public static HoldOptions FromConfiguration(IConfiguration cfg) => new()
    {
        DurationSeconds = ReadPositive(cfg["HOLD_DURATION_SECONDS"], DefaultDurationSeconds),
        SweepIntervalSeconds = ReadPositive(cfg["EXPIRY_SWEEP_INTERVAL_SECONDS"], 5),
    };

    private static int ReadPositive(string? raw, int fallback) =>
        int.TryParse(raw, out var v) && v > 0 ? v : fallback;
}
