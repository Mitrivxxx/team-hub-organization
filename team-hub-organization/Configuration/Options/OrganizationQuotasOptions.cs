using System.ComponentModel.DataAnnotations;

namespace team_hub_organization.Configuration.Options;

public sealed class OrganizationQuotasOptions
{
    public const string SectionName = "Quotas";

    [Range(1, 10_000)]
    public int MaxOrgsPerUser { get; set; } = 5;

    [Range(1, 100_000)]
    public int MaxMembersPerOrg { get; set; } = 100;

    [Range(1, 10_000)]
    public int MaxTeamsPerOrg { get; set; } = 50;

    [Range(1, 10_000)]
    public int MaxPendingInvitesPerOrg { get; set; } = 50;
}
