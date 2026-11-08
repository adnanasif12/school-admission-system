using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Npgsql;

namespace SchoolAdmission.Tests;

/// <summary>
/// Creates a throw-away PostgreSQL database per test class. Point TEST_DB_ADMIN at any Postgres server
/// (default: localhost:5432, postgres/postgres). `docker compose -f docker-compose.test.yml` provides one.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private static string AdminConnection =>
        Environment.GetEnvironmentVariable("TEST_DB_ADMIN")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    public string Name { get; } = "sa_test_" + Guid.NewGuid().ToString("N")[..12];

    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(AdminConnection) { Database = Name, Pooling = true, MaxPoolSize = 80 }.ToString();

    public static async Task<TestDatabase> CreateAsync()
    {
        var db = new TestDatabase();
        await using var conn = new NpgsqlConnection(AdminConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{db.Name}\"", conn);
        await cmd.ExecuteNonQueryAsync();
        return db;
    }

    public async Task<string?> ReadHoldStatusAsync(string holdId)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT \"Status\" FROM holds WHERE \"Id\" = @id", conn);
        cmd.Parameters.AddWithValue("id", holdId);
        return await cmd.ExecuteScalarAsync() as string;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(AdminConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>One throw-away database shared by the tests of a class (tests create their own classes/students).</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public TestDatabase Db { get; private set; } = null!;
    public async Task InitializeAsync() => Db = await TestDatabase.CreateAsync();
    public async Task DisposeAsync() => await Db.DisposeAsync();
}

/// <summary>One running copy of the real API (own Kestrel-less test server) bound to the given database.</summary>
public sealed class ApiInstance : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly int _holdSeconds;
    private readonly int _sweepSeconds;

    public ApiInstance(string connectionString, int holdSeconds = 600, int sweepSeconds = 1)
    {
        _connectionString = connectionString;
        _holdSeconds = holdSeconds;
        _sweepSeconds = sweepSeconds;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", _holdSeconds.ToString());
        builder.UseSetting("EXPIRY_SWEEP_INTERVAL_SECONDS", _sweepSeconds.ToString());
        // Config (not ConfigureLogging) because appsettings.json sets Logging:LogLevel and wins over code.
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft", "Warning");
    }
}

public static class Api
{
    public static string Student(int n) => (20150000000000000L + n).ToString();

    /// <summary>A random 17-digit id, for tests that read per-student lists across classes (/me/holds).</summary>
    public static string UniqueStudent() => (30000000000000000L + Random.Shared.NextInt64(0, 60000000000000000L)).ToString();

    public static async Task<string> CreateClassAsync(HttpClient c, int seats, string name = "Class 6 - Morning Shift")
    {
        var res = await c.PostAsJsonAsync("/classes", new { name, totalSeats = seats });
        res.EnsureSuccessStatusCode();
        return (await Json(res)).GetProperty("id").GetString()!;
    }

    public static Task<HttpResponseMessage> HoldAsync(HttpClient c, string classId, string studentId) =>
        Send(c, HttpMethod.Post, $"/classes/{classId}/holds", studentId);

    public static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string url, string? studentId)
    {
        var req = new HttpRequestMessage(method, url);
        if (studentId is not null) req.Headers.Add("X-Student-Id", studentId);
        return await c.SendAsync(req);
    }

    public static async Task<JsonElement> Json(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    public static async Task<JsonElement> ClassAsync(HttpClient c, string classId) =>
        await Json(await c.GetAsync($"/classes/{classId}"));
}
