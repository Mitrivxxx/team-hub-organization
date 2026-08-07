using System.ComponentModel.DataAnnotations;

namespace team_hub_organization.Configuration.Options;

public sealed class OrganizationLifecycleOptions
{
    public const string SectionName = "OrganizationLifecycle";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    [Range(1, 3650)]
    public int ActivityTtlDays { get; set; } = 90;

    [Range(1, 3650)]
    public int ImportArtifactTtlDays { get; set; } = 14;

    [Range(1, 168)]
    public int IdempotencyTtlHours { get; set; } = 24;

    [Range(1, 1440)]
    public int PurgeIntervalMinutes { get; set; } = 60;
}
