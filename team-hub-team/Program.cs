using DotNetEnv;
using TeamHub.Observability;
using team_hub_team.Configuration;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Host.AddTeamHubSerilog();

builder.Services.AddTeamHubOpenTelemetry(builder.Configuration, "team-hub-team");
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseApiPipeline();
app.MapTeamHubObservabilityEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
