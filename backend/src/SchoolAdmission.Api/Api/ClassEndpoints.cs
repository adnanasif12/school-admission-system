using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Api;

public static class ClassEndpoints
{
    public static void MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes", async (HttpRequest req, ClassService svc, CancellationToken ct) =>
        {
            var body = await RequestHelpers.ReadJsonAsync<CreateClassRequest>(req, ct);
            var created = await svc.CreateAsync(body, ct);
            return Results.Created($"/classes/{created.Id}", created);
        });

        app.MapGet("/classes", async (ClassService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)));

        app.MapGet("/classes/{classId}", async (string classId, ClassService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(classId, ct)));
    }
}
