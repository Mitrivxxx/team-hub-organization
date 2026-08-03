using team_hub_organization.Seeding.Abstractions;
using team_hub_organization.Seeding.Internal;

namespace team_hub_organization.Seeding.Staging;

public sealed class StagingDataSeeder(
    OrganizationDemoBuilder builder,
    ILogger<StagingDataSeeder> logger) : IEnvironmentDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Running Staging organization data seed...");
        await builder.SeedAsync(cancellationToken);
        logger.LogInformation("Staging organization data seed finished");
    }
}
