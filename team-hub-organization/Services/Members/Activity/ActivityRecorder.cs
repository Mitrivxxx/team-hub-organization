using System.Text.Json;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Members.Activity;

public interface IActivityRecorder
{
    void Record(
        Guid organizationId,
        string type,
        Guid? actorUserId,
        Guid? targetUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        object? details = null,
        DateTimeOffset? occurredAt = null);
}

public sealed class ActivityRecorder(OrganizationDbContext db) : IActivityRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public void Record(
        Guid organizationId,
        string type,
        Guid? actorUserId,
        Guid? targetUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        object? details = null,
        DateTimeOffset? occurredAt = null)
    {
        db.OrganizationActivities.Add(new OrganizationActivity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Type = type,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonOptions),
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow
        });
    }
}
