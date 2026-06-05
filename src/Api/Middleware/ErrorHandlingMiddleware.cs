using Api.Models;
using System.Net;
using System.Text.Json;

namespace Api.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteError(ctx, ex);
        }
    }

    private static Task WriteError(HttpContext ctx, Exception ex)
    {
        var (status, message) = ex switch
        {
            KeyNotFoundException     => (HttpStatusCode.NotFound,           "Resource not found"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,    "Unauthorized"),
            ArgumentException        => (HttpStatusCode.BadRequest,          ex.Message),
            _                        => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        ctx.Response.StatusCode  = (int)status;
        ctx.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new ErrorResponse(message), _json);
        return ctx.Response.WriteAsync(body);
    }
}
