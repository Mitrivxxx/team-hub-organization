using DotNetEnv;
using TeamHub.Observability;
using team_hub_organization.Configuration.Extensions;
using team_hub_organization.Seeding;

// Local/dev convenience only. Production gets config from appsettings + compose env_file / Aspire.
if (!string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        Environments.Production,
        StringComparison.OrdinalIgnoreCase))
{
    // NoClobber: Aspire-injected ConnectionStrings/Kafka/Jwt win over local .env (Port=5433).
    Env.NoClobber().TraversePath().Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Host.AddTeamHubSerilog();

builder.Services.AddTeamHubOpenTelemetry(builder.Configuration, "team-hub-organization", includeEntityFrameworkCore: true);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddOrganizationHealthChecks(builder.Configuration);
builder.Services.AddJwtConfiguration(builder.Configuration);
builder.Services.AddOrganizationBlobStorage(builder.Configuration, builder.Environment);
builder.Services.AddOrganizationGrpc(builder.Configuration);
builder.Services.AddImportExportJobs();
builder.Services.AddOrganizationLifecycle(builder.Configuration);
builder.Services.AddQuotas(builder.Configuration);
builder.Services.AddOrganizationKafka(builder.Configuration);
builder.Services.AddApiInfrastructure();
builder.Services.AddValidation();
builder.Services.AddApplicationServices();
builder.Services.AddDemoSeeding(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.Contains("--seed"))
{
    await app.RunSeedAndExitAsync();
    return;
}

await app.ApplyStartupSchemaAsync();

app.UseApiPipeline();
app.MapTeamHubObservabilityEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
