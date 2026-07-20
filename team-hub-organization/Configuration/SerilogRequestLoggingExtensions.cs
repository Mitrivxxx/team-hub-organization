using Serilog;

namespace team_hub_organization.Configuration;

public static class SerilogRequestLoggingExtensions
{
    public static WebApplication UseSerilogRequestLoggingExcludingHealth(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, _, ex) =>
            {
                if (ex is not null)
                    return Serilog.Events.LogEventLevel.Error;

                var path = httpContext.Request.Path;
                if (path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics"))
                    return Serilog.Events.LogEventLevel.Verbose;

                return Serilog.Events.LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
                    diagnosticContext.Set("CorrelationId", correlationId);
            };
        });

        return app;
    }
}
