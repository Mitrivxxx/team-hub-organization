using Serilog;
using Serilog.Events;

namespace team_hub_organization.Configuration;

public static class SerilogRequestLoggingExtensions
{
    public static IApplicationBuilder UseSerilogRequestLoggingExcludingHealth(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (context, _, exception) =>
                exception is not null
                    ? LogEventLevel.Error
                    : context.Request.Path.StartsWithSegments("/health")
                        || context.Request.Path.StartsWithSegments("/metrics")
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information;
        });
}
