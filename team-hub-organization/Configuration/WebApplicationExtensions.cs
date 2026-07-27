using Asp.Versioning.ApiExplorer;
using team_hub_organization.Grpc;

namespace team_hub_organization.Configuration;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<UserIdLoggingMiddleware>();
        app.UseSerilogRequestLoggingExcludingHealth();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        $"Team Hub Organization API {description.GroupName}");
                }
            });
        }

        app.MapHealthChecks("/health");
        app.MapControllers();
        app.MapGrpcService<OrganizationMemberGrpcService>();

        return app;
    }
}
