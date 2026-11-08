using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchoolAdmission.Api.Api;

public static partial class RequestHelpers
{
    public const string StudentHeader = "X-Student-Id";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[0-9]{17}$")]
    private static partial Regex SeventeenDigits();

    /// <summary>Returns the validated 17-digit student id, or throws a 400.</summary>
    public static string RequireStudentId(HttpRequest req) =>
        ReadStudentId(req) ?? throw new ApiException(400, "MISSING_STUDENT_ID",
            "Please enter your 17-digit birth registration number first.");

    /// <summary>Null when the header is absent; throws a 400 when present but malformed.</summary>
    public static string? ReadStudentId(HttpRequest req)
    {
        if (!req.Headers.TryGetValue(StudentHeader, out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;
        var id = raw.ToString().Trim();
        if (!SeventeenDigits().IsMatch(id))
            throw new ApiException(400, "INVALID_STUDENT_ID",
                "The student ID must be exactly 17 digits (your birth registration number).");
        return id;
    }

    /// <summary>Reads a JSON body regardless of content-type; bad or empty JSON becomes a 400.</summary>
    public static async Task<T> ReadJsonAsync<T>(HttpRequest req, CancellationToken ct) where T : class
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<T>(req.Body, WebJson, ct);
            return body ?? throw new ApiException(400, "BAD_REQUEST", "A JSON request body is required.");
        }
        catch (JsonException)
        {
            throw new ApiException(400, "BAD_REQUEST",
                "The request body is not valid. Check that every value has the right type (for example, seats must be a whole number).");
        }
    }

    public static string Iso(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
}
