namespace SchoolAdmission.Api.Api;

/// <summary>An expected failure that maps to an HTTP status and a plain-language JSON body.</summary>
public class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static class ErrorHandling
{
    /// <summary>Turns exceptions into <c>{ "error": CODE, "message": "..." }</c> so clients never get an empty body.</summary>
    public static void UseApiErrorHandling(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (ApiException ex)
            {
                await Write(ctx, ex.Status, ex.Code, ex.Message);
            }
            catch (BadHttpRequestException)
            {
                await Write(ctx, 400, "BAD_REQUEST", "The request could not be read. Please check the values you sent.");
            }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
            {
                // client went away; nothing to write
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Unhandled error for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
                if (!ctx.Response.HasStarted)
                    await Write(ctx, 500, "SERVER_ERROR", "Something went wrong on our side. Please try again in a moment.");
            }
        });
    }

    private static Task Write(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { error = code, message });
    }
}
