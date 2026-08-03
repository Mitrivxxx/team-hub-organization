namespace team_hub_organization.Seeding.Abstractions;

public interface IEnvironmentDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
