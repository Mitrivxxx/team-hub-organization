using System.Diagnostics;
using Serilog.Context;

namespace team_hub_team.Configuration;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var incomingHeaderValue = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId =
            !string.IsNullOrWhiteSpace(traceId)
                ? traceId
                : !string.IsNullOrWhiteSpace(incomingHeaderValue)
                    ? incomingHeaderValue
                    : Guid.NewGuid().ToString("N");

        context.Request.Headers[HeaderName] = correlationId;
        context.Items[ItemKey] = correlationId;
        context.Response.Headers.Append(HeaderName, correlationId);

        using (LogContext.PushProperty(ItemKey, correlationId))
        {
            await next(context);
        }
    }
}
