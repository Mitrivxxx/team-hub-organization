namespace team_hub_organization.Dtos;

public sealed class ActivityItemResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public Guid? ActorUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class ActivityPageResponse
{
    public IReadOnlyList<ActivityItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
