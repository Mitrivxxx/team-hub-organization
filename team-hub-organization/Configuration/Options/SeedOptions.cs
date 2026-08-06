using System.ComponentModel.DataAnnotations;

namespace team_hub_organization.Configuration.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; init; }

    [Required]
    public string OwnerUsername { get; init; } = "JanWilk123";

    [Range(0, 10)]
    public int OrganizationCount { get; init; }

    [Range(0, 10_000)]
    public int MembersPerOrganization { get; init; }

    [Range(0, 20)]
    public int TeamsPerOrganization { get; init; }

    [Range(0, 100)]
    public int AdminCountPerOrganization { get; init; } = 2;

    [Range(0, 20)]
    public int PendingInvitationCount { get; init; } = 2;

    [Required]
    public string SlugPrefix { get; init; } = "demo-org";

    /// <summary>Base 10-digit NIP; org index is added (must stay 10 digits).</summary>
    [Required]
    [RegularExpression(@"^\d{10}$")]
    public string NipBase { get; init; } = "5000000000";
}
