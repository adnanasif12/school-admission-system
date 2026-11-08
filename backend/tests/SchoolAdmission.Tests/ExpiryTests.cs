using System.Net;
using Xunit;

namespace SchoolAdmission.Tests;

public class ExpiryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private TestDatabase _db = null!;
    private ApiInstance _api = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _db = fixture.Db;
        // 1-second holds, background sweep every second.
        _api = new ApiInstance(_db.ConnectionString, holdSeconds: 1, sweepSeconds: 1);
        _client = _api.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task ExpiredHold_ReleasesSeat_WithoutAnyApiCall_AndCannotBeConfirmed()
    {
        var classId = await Api.CreateClassAsync(_client, 1);
        var holdId = (await Api.Json(await Api.HoldAsync(_client, classId, Api.Student(1)))).GetProperty("holdId").GetString()!;
        Assert.Equal(0, (await Api.ClassAsync(_client, classId)).GetProperty("availableSeats").GetInt32());

        // Wait for expiry and for the sweeper. We call NO hold endpoint meanwhile.
        string? stored = null;
        for (var i = 0; i < 40 && stored != "EXPIRED"; i++)
        {
            await Task.Delay(250);
            stored = await _db.ReadHoldStatusAsync(holdId);
        }
        Assert.Equal("EXPIRED", stored); // flipped by the background service, not by a request

        var cls = await Api.ClassAsync(_client, classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());

        var confirm = await Api.Send(_client, HttpMethod.Post, $"/holds/{holdId}/confirm", Api.Student(1));
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("HOLD_EXPIRED", (await Api.Json(confirm)).GetProperty("error").GetString());

        // The freed seat can be taken by someone else, and the same student may re-apply.
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(_client, classId, Api.Student(2))).StatusCode);
    }

    [Fact]
    public async Task LapsedHold_NeverCounts_EvenBeforeTheSweeperRuns()
    {
        // Use a very slow sweeper so only the query-time expiry check can free the seat.
        // Own database: the fixture's 1s sweeper must not touch this test's rows.
        await using var db = await TestDatabase.CreateAsync();
        await using var slow = new ApiInstance(db.ConnectionString, holdSeconds: 1, sweepSeconds: 3600);
        var client = slow.CreateClient();
        var classId = await Api.CreateClassAsync(client, 1);
        var holdId = (await Api.Json(await Api.HoldAsync(client, classId, Api.Student(1)))).GetProperty("holdId").GetString()!;

        await Task.Delay(1500);

        Assert.Equal("HELD", await db.ReadHoldStatusAsync(holdId)); // still HELD in the table...
        Assert.Equal(1, (await Api.ClassAsync(client, classId)).GetProperty("availableSeats").GetInt32()); // ...but not counted
        var state = await Api.Json(await client.GetAsync($"/holds/{holdId}"));
        Assert.Equal("EXPIRED", state.GetProperty("status").GetString());

        // Same student re-applies although the stale row is still HELD (unique index must not block it).
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(client, classId, Api.Student(1))).StatusCode);
    }
}
