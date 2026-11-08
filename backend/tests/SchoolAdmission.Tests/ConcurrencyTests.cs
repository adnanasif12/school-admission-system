using System.Net;
using Xunit;

namespace SchoolAdmission.Tests;

public class ConcurrencyTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private ApiInstance _api = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        _api = new ApiInstance(_db.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ManyParallelHolds_OnFewSeats_ExactlyTotalSeatsSucceed()
    {
        const int seats = 5, requests = 60;
        var client = _api.CreateClient();
        var classId = await Api.CreateClassAsync(client, seats);

        // 60 different students hit the same class at the same instant.
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(1, requests).Select(async i =>
        {
            await gate.Task;
            return (await Api.HoldAsync(client, classId, Api.Student(i))).StatusCode;
        }).ToList();
        gate.SetResult();
        var codes = await Task.WhenAll(tasks);

        Assert.Equal(seats, codes.Count(c => c == HttpStatusCode.Created));
        Assert.Equal(requests - seats, codes.Count(c => c == HttpStatusCode.Conflict));

        var cls = await Api.ClassAsync(client, classId);
        Assert.Equal(seats, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task SameStudent_ParallelHolds_OnlyOneIsCreated()
    {
        var client = _api.CreateClient();
        var classId = await Api.CreateClassAsync(client, 50);

        var codes = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(async _ => (await Api.HoldAsync(client, classId, Api.Student(1))).StatusCode));

        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.Created));
        Assert.Equal(11, codes.Count(c => c == HttpStatusCode.Conflict));
        Assert.Equal(1, (await Api.ClassAsync(client, classId)).GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task TwoBackendProcesses_OnOneDatabase_NeverExceedTotalSeats()
    {
        // Bonus: two independent app instances (separate DI containers, separate connection pools)
        // sharing one database, hammered at once. Only the Postgres row lock can keep this correct.
        const int seats = 7, requests = 60;
        await using var second = new ApiInstance(_db.ConnectionString);
        var a = _api.CreateClient();
        var b = second.CreateClient();
        var classId = await Api.CreateClassAsync(a, seats);

        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(1, requests).Select(async i =>
        {
            await gate.Task;
            var client = i % 2 == 0 ? a : b;
            return (await Api.HoldAsync(client, classId, Api.Student(i))).StatusCode;
        }).ToList();
        gate.SetResult();
        var codes = await Task.WhenAll(tasks);

        Assert.Equal(seats, codes.Count(c => c == HttpStatusCode.Created));
        var viaA = await Api.ClassAsync(a, classId);
        var viaB = await Api.ClassAsync(b, classId);
        Assert.Equal(seats, viaA.GetProperty("heldSeats").GetInt32() + viaA.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(viaA.ToString(), viaB.ToString());
    }
}
