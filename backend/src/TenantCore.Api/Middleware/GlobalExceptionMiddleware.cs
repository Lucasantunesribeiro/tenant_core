using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteProblemDetailsAsync(context, ex.StatusCode, ex.Type, ex.Title, ex.Message, ex.Errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "server_error",
                "Internal server error",
                "An unexpected error occurred.");
        }
    }

    private static Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string type,
        string title,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        var payload = new Dictionary<string, object?>
        {
            ["type"] = problemDetails.Type,
            ["title"] = problemDetails.Title,
            ["status"] = problemDetails.Status,
            ["detail"] = problemDetails.Detail,
            ["instance"] = problemDetails.Instance,
            ["traceId"] = context.TraceIdentifier
        };

        if (errors is not null && errors.Count > 0)
        {
            payload["errors"] = errors;
        }

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
