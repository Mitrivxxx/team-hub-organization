namespace team_hub_organization.Models;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string? PartitionKey { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
