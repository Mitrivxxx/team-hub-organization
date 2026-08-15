using Microsoft.Extensions.Hosting;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Seeding.Abstractions;
using team_hub_organization.Seeding.Internal;

namespace team_hub_organization.Seeding;

public static class SeedServiceCollectionExtensions
{
    public static IServiceCollection AddDemoSeeding(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (environment.IsProduction())
            return services;

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Staging"))
            return services;

        services.AddScoped<OrganizationDemoBuilder>();
        services.AddScoped<IEnvironmentDataSeeder, DemoDataSeeder>();

        return services;
    }

    public static bool CanRunDemoSeed(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsProduction())
            return false;

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Staging"))
            return false;

        return configuration.GetValue($"{SeedOptions.SectionName}:Enabled", false);
    }
}
