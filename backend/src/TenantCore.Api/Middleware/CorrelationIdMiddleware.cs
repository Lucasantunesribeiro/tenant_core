using TenantCore.Application.Common.Security;

namespace TenantCore.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var headerValue) &&
                            !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[HeaderNames.CorrelationId] = correlationId;
        context.Response.Headers[HeaderNames.CorrelationId] = correlationId;

        await next(context);
    }
}
