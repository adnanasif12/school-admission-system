namespace SchoolAdmission.Api.Api;

public record CreateClassRequest(string? Name, int? TotalSeats);

// Field names below are the public contract (camelCase on the wire). Do not rename or remove.
public record ClassCreatedResponse(string Id, string Name, int TotalSeats);

public record ClassResponse(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public record HoldCreatedResponse(string HoldId, string ClassId, string StudentId, string Status, string ExpiresAt);

public record HoldResponse(string HoldId, string ClassId, string ClassName, string StudentId, string Status, string ExpiresAt);

public record ConfirmResponse(string HoldId, string Status);

public record MyHoldResponse(string HoldId, string ClassId, string ClassName, string Status, string ExpiresAt);
