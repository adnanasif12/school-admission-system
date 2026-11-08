using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Api;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database connection comes from the environment: ConnectionStrings__Default
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(HoldOptions.FromConfiguration(builder.Configuration)));
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<HoldService>();
builder.Services.AddHostedService<HoldExpiryService>();

// The React app is served from a different origin than the API.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseMigrator.MigrateAsync(app.Services);

app.UseCors();
app.UseApiErrorHandling();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapClassEndpoints();
app.MapHoldEndpoints();

app.Run();

// Exposed so the integration tests can boot the app with WebApplicationFactory.
public partial class Program;
