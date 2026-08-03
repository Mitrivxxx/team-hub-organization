using team_hub_organization.Seeding.Abstractions;
using team_hub_organization.Seeding.Internal;

namespace team_hub_organization.Seeding.Development;

public sealed class DevelopmentDataSeeder(
    OrganizationDemoBuilder builder,
    ILogger<DevelopmentDataSeeder> logger) : IEnvironmentDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Running Development organization data seed...");
        await builder.SeedAsync(cancellationToken);
        logger.LogInformation("Development organization data seed finished");
    }
}
