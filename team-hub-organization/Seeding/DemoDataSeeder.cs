using team_hub_organization.Seeding.Abstractions;
using team_hub_organization.Seeding.Internal;

namespace team_hub_organization.Seeding;

public sealed class DemoDataSeeder(
    OrganizationDemoBuilder builder,
    ILogger<DemoDataSeeder> logger) : IEnvironmentDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Running organization demo data seed...");
        await builder.SeedAsync(cancellationToken);
        logger.LogInformation("Organization demo data seed finished");
    }
}
