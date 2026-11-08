using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Connection string comes from the environment: ConnectionStrings__Default
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connectionString));

var app = builder.Build();

await DatabaseMigrator.MigrateAsync(app.Services);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so the integration tests can boot the app with WebApplicationFactory.
public partial class Program;
