using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Api;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database connection comes from the environment: ConnectionStrings__Default.
// Configuration is read lazily (inside the factories) so test hosts can override it.
builder.Services.AddDbContext<AdmissionDbContext>((sp, o) => o.UseNpgsql(
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => Microsoft.Extensions.Options.Options.Create(
    HoldOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>())));
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<HoldService>();
builder.Services.AddHostedService<HoldExpiryService>();

// The React app is served from a different origin than the API.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseMigrator.MigrateAsync(app.Services);

app.UseCors();
app.UseApiErrorHandling();

// Friendly landing response so opening the API URL in a browser is not a blank page.
app.MapGet("/", () => Results.Ok(new
{
    name = "School Admission System API",
    status = "running",
    note = "This is the JSON API. The web app is served separately by the frontend container.",
    health = "/health",
    endpoints = new[]
    {
        "POST /classes", "GET /classes", "GET /classes/{classId}",
        "POST /classes/{classId}/holds", "GET /holds/{holdId}",
        "POST /holds/{holdId}/confirm", "DELETE /holds/{holdId}", "GET /me/holds",
    },
}));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapClassEndpoints();
app.MapHoldEndpoints();

app.Run();

// Exposed so the integration tests can boot the app with WebApplicationFactory.
public partial class Program;
