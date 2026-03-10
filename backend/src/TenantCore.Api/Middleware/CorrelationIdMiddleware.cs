using System.Text.RegularExpressions;
using TenantCore.Application.Common.Security;

namespace TenantCore.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    // Only alphanumeric and hyphens, max 64 chars — prevents log injection via newlines or control chars
    private static readonly Regex SafeCorrelationId = new(@"^[a-zA-Z0-9\-]{1,64}$", RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var headerValue) &&
                            !string.IsNullOrWhiteSpace(headerValue) &&
                            SafeCorrelationId.IsMatch(headerValue.ToString())
            ? headerValue.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[HeaderNames.CorrelationId] = correlationId;
        context.Response.Headers[HeaderNames.CorrelationId] = correlationId;

        await next(context);
    }
}
