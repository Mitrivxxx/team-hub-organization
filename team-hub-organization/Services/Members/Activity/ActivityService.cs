using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.Activity;

public interface IActivityService
{
    Task<ActivityPageResponse> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        string? type = null,
        string? q = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}

public sealed class ActivityService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz) : IActivityService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<ActivityPageResponse> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        string? type = null,
        string? q = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        if (page < 1)
            page = 1;
        if (pageSize < 1)
            pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        var query = db.OrganizationActivities.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.Type == type);

        if (from is not null)
            query = query.Where(a => a.OccurredAt >= from);

        if (to is not null)
            query = query.Where(a => a.OccurredAt <= to);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.Type.ToLower().Contains(term)
                || (a.Details != null && a.Details.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityItemResponse
            {
                Id = a.Id,
                Type = a.Type,
                ActorUserId = a.ActorUserId,
                TargetUserId = a.TargetUserId,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                OccurredAt = a.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return new ActivityPageResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
