using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using TeamHub.Observability;
using team_hub_organization.Configuration;
using team_hub_organization.Data;
using team_hub_organization.Services.Rbac;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Host.AddTeamHubSerilog();

builder.Services.AddTeamHubOpenTelemetry(builder.Configuration, "team-hub-organization", includeEntityFrameworkCore: true);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddOrganizationHealthChecks(builder.Configuration);
builder.Services.AddJwtConfiguration(builder.Configuration);
builder.Services.AddApiInfrastructure();
builder.Services.AddValidation();
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
    db.Database.Migrate();
    await scope.ServiceProvider.GetRequiredService<IPermissionSeedService>().EnsureCatalogAsync();
}

app.UseApiPipeline();
app.MapTeamHubObservabilityEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
