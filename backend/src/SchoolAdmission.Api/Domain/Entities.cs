namespace SchoolAdmission.Api.Domain;

/// <summary>Lifecycle of a seat hold (an "application").</summary>
public enum HoldStatus
{
    HELD,
    CONFIRMED,
    EXPIRED,
    CANCELLED,
}

/// <summary>One shift of one grade, e.g. "Class 6 - Morning Shift". Each shift is its own class.</summary>
public class SchoolClass
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Hold
{
    public string Id { get; set; } = default!;
    public string ClassId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public SchoolClass? Class { get; set; }
}
