using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TeamHub.Observability.Middleware;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Members.Audit;

public interface IAuditRecorder
{
    void Record(
        Guid organizationId,
        string action,
        Guid? actorUserId,
        Guid? targetUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        object? details = null,
        DateTimeOffset? occurredAt = null);
}

public sealed class AuditRecorder(
    OrganizationDbContext db,
    IHttpContextAccessor httpContextAccessor) : IAuditRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public void Record(
        Guid organizationId,
        string action,
        Guid? actorUserId,
        Guid? targetUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        object? details = null,
        DateTimeOffset? occurredAt = null)
    {
        string? correlationId = null;
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var item) == true)
            correlationId = item?.ToString();

        db.OrganizationAuditEvents.Add(new OrganizationAuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            ActorUserId = actorUserId,
            Action = action,
            TargetUserId = targetUserId,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonOptions),
            CorrelationId = correlationId
        });
    }
}
