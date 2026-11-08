using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Api;

public static class HoldEndpoints
{
    public static void MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        // 4. Apply (seat hold)
        app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest req, HoldService svc, CancellationToken ct) =>
        {
            var studentId = RequestHelpers.RequireStudentId(req);
            var hold = await svc.CreateAsync(classId, studentId, ct);
            return Results.Created($"/holds/{hold.HoldId}", hold);
        });

        // 5. Current state of one application. X-Student-Id is optional here, but must match when sent.
        app.MapGet("/holds/{holdId}", async (string holdId, HttpRequest req, HoldService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(holdId, RequestHelpers.ReadStudentId(req), ct)));

        // 6. Pay the fee -> confirm
        app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest req, HoldService svc, CancellationToken ct) =>
            Results.Ok(await svc.ConfirmAsync(holdId, RequestHelpers.RequireStudentId(req), ct)));

        // 7. Cancel
        app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest req, HoldService svc, CancellationToken ct) =>
        {
            await svc.CancelAsync(holdId, RequestHelpers.RequireStudentId(req), ct);
            return Results.NoContent();
        });

        // 8. All applications of this student
        app.MapGet("/me/holds", async (HttpRequest req, HoldService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMineAsync(RequestHelpers.RequireStudentId(req), ct)));
    }
}
