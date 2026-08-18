using Asp.Versioning.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using TeamHub.Observability;
using team_hub_organization.Configuration.Middleware;
using team_hub_organization.Data;
using team_hub_organization.Grpc;
using team_hub_organization.Seeding;
using team_hub_organization.Seeding.Abstractions;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Configuration.Extensions;

public static class WebApplicationExtensions
{
    public static async Task RunSeedAndExitAsync(this WebApplication app)
    {
        if (!SeedServiceCollectionExtensions.CanRunDemoSeed(app.Environment, app.Configuration))
            throw new InvalidOperationException(
                "Demo seed is only allowed when ASPNETCORE_ENVIRONMENT is Development or Staging and Seed:Enabled is true.");

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IPermissionSeedService>().EnsureCatalogAsync();
        await scope.ServiceProvider.GetRequiredService<IEnvironmentDataSeeder>().SeedAsync();
    }
    public static async Task ApplyStartupSchemaAsync(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IPermissionSeedService>().EnsureCatalogAsync();
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseTeamHubExceptionHandling();
        app.UseTeamHubCorrelationId();
        app.UseTeamHubSessionId();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<IdempotencyMiddleware>();
        app.UseTeamHubUserIdLogging();
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
