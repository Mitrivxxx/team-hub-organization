using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using TeamHub.Observability;
using team_hub_organization.Configuration;
using team_hub_organization.Data;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Host.AddTeamHubSerilog();

builder.Services.AddTeamHubOpenTelemetry(builder.Configuration, "team-hub-organization", includeEntityFrameworkCore: true);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddOrganizationHealthChecks(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.Migrate();
    }
}

app.UseApiPipeline();
app.MapTeamHubObservabilityEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
