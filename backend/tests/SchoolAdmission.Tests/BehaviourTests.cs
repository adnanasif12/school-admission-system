using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SchoolAdmission.Tests;

public class BehaviourTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private TestDatabase _db = null!;
    private ApiInstance _api = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _db = fixture.Db;
        _api = new ApiInstance(_db.ConnectionString);
        _client = _api.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task Confirm_IsIdempotent_AndCountsOnce()
    {
        var classId = await Api.CreateClassAsync(_client, 3);
        var hold = await Api.Json(await Api.HoldAsync(_client, classId, Api.Student(1)));
        var holdId = hold.GetProperty("holdId").GetString()!;

        var first = await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", Api.Student(1));
        var second = await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", Api.Student(1));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("CONFIRMED", (await Api.Json(second)).GetProperty("status").GetString());

        var cls = await Api.ClassAsync(_client, classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Confirm_ByAnotherStudent_Is403_AndMissingHeaderIs400()
    {
        var classId = await Api.CreateClassAsync(_client, 3);
        var holdId = (await Api.Json(await Api.HoldAsync(_client, classId, Api.Student(1)))).GetProperty("holdId").GetString()!;

        var other = await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", Api.Student(2));
        var none = await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", null);
        var bad = await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", "12345");
        var missing = await Api.Send(_client, HttpMethod.Post, "/holds/h_nope/confirm", Api.Student(1));

        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Cancel_FreesTheSeat_AndIsRepeatable()
    {
        var classId = await Api.CreateClassAsync(_client, 1);
        var holdId = (await Api.Json(await Api.HoldAsync(_client, classId, Api.Student(1)))).GetProperty("holdId").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await Api.HoldAsync(_client, classId, Api.Student(2))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Api.Send(_client, HttpMethod.Delete, $"/holds/{holdId}", Api.Student(2))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Api.Send(_client, HttpMethod.Delete, $"/holds/{holdId}", Api.Student(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Api.Send(_client, HttpMethod.Delete, $"/holds/{holdId}", Api.Student(1))).StatusCode);

        var state = await Api.Json(await _client.GetAsync($"/holds/{holdId}"));
        Assert.Equal("CANCELLED", state.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(_client, classId, Api.Student(2))).StatusCode);

        // A cancelled application can no longer be paid.
        Assert.Equal(HttpStatusCode.Conflict, (await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", Api.Student(1))).StatusCode);
    }

    [Fact]
    public async Task OneActiveHoldPerStudentPerClass_ButAnotherClassIsFine()
    {
        var student = Api.UniqueStudent();
        var a = await Api.CreateClassAsync(_client, 5, "Class 6 - Morning Shift");
        var b = await Api.CreateClassAsync(_client, 5, "Class 6 - Day Shift");

        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(_client, a, student)).StatusCode);
        var dup = await Api.HoldAsync(_client, a, student);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("ALREADY_APPLIED", (await Api.Json(dup)).GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(_client, b, student)).StatusCode);

        var mine = await Api.Json(await Api.Send(_client, HttpMethod.Get, "/me/holds", student));
        Assert.Equal(2, mine.GetArrayLength());
        Assert.All(mine.EnumerateArray(), h =>
        {
            Assert.True(h.TryGetProperty("holdId", out _));
            Assert.True(h.TryGetProperty("classId", out _));
            Assert.True(h.TryGetProperty("className", out _));
            Assert.Equal("HELD", h.GetProperty("status").GetString());
            Assert.True(h.TryGetProperty("expiresAt", out _));
        });
    }

    [Theory]
    [InlineData("{\"name\":\"\",\"totalSeats\":10}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":0}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":-4}")]
    [InlineData("{\"name\":\"X\"}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":\"many\"}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":12.5}")]
    [InlineData("not json")]
    public async Task CreateClass_WithBadInput_Is400WithMessage(string body)
    {
        var res = await _client.PostAsync("/classes", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await Api.Json(res)).GetProperty("message").GetString()));
    }

    [Fact]
    public async Task ApiContract_FieldNamesAndStatusCodes()
    {
        var created = await _client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var c = await Api.Json(created);
        Assert.Equal(new[] { "id", "name", "totalSeats" }, c.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray());
        Assert.StartsWith("c_", c.GetProperty("id").GetString());
        var classId = c.GetProperty("id").GetString()!;

        var one = await Api.ClassAsync(_client, classId);
        foreach (var f in new[] { "id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats" })
            Assert.True(one.TryGetProperty(f, out _), f);

        var list = await Api.Json(await _client.GetAsync("/classes"));
        Assert.Equal(JsonValueKindArray, list.ValueKind);

        var holdRes = await Api.HoldAsync(_client, classId, Api.Student(9));
        Assert.Equal(HttpStatusCode.Created, holdRes.StatusCode);
        var h = await Api.Json(holdRes);
        foreach (var f in new[] { "holdId", "classId", "studentId", "status", "expiresAt" })
            Assert.True(h.TryGetProperty(f, out _), f);
        Assert.StartsWith("h_", h.GetProperty("holdId").GetString());
        Assert.EndsWith("Z", h.GetProperty("expiresAt").GetString());

        var got = await Api.Json(await _client.GetAsync($"/holds/{h.GetProperty("holdId").GetString()}"));
        foreach (var f in new[] { "holdId", "classId", "status", "expiresAt" })
            Assert.True(got.TryGetProperty(f, out _), f);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/classes/c_missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.HoldAsync(_client, "c_missing", Api.Student(1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.HoldAsync(_client, classId, "abc")).StatusCode);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindArray = System.Text.Json.JsonValueKind.Array;
}
