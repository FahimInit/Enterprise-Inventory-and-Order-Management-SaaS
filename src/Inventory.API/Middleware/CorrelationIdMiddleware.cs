using Inventory.Application.Common;
using Serilog.Context;

namespace Inventory.API.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
                            !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        tenantContext.SetRequestMetadata(correlationId, ipAddress);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("ClientIp", ipAddress))
        {
            await _next(context);
        }
    }
}
