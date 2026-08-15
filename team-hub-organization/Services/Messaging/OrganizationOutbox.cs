using System.Text.Json;
using TeamHub.Kafka.Events;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Messaging;

public interface IOrganizationOutbox
{
    void EnqueueMemberAdded(OrganizationMemberAddedEvent message, string? partitionKey = null);
}

public sealed class OrganizationOutbox(OrganizationDbContext db) : IOrganizationOutbox
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void EnqueueMemberAdded(OrganizationMemberAddedEvent message, string? partitionKey = null)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = TeamHub.Kafka.KafkaTopics.OrganizationEvents,
            PartitionKey = partitionKey ?? message.OrganizationId.ToString(),
            EventType = message.EventType,
            Payload = JsonSerializer.Serialize(message, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        });
    }
}
