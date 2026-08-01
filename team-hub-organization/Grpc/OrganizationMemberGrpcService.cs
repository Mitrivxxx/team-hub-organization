using Grpc.Core;
using TeamHub.GrpcContracts.Organization.V1;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Grpc;

public sealed class OrganizationMemberGrpcService(
    IMemberService memberService,
    IActivityService activityService)
    : OrganizationMemberService.OrganizationMemberServiceBase
{
    /// <summary>List organization members for an authenticated actor.</summary>
    public override async Task<ListMembersResponse> ListMembers(
        ListMembersRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrganizationId, out var organizationId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid organization_id."));

        if (!Guid.TryParse(request.ActorUserId, out var actorUserId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid actor_user_id."));

        Guid? roleId = null;
        if (request.HasRoleId)
        {
            if (!Guid.TryParse(request.RoleId, out var parsedRoleId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role_id."));
            roleId = parsedRoleId;
        }

        Guid? teamId = null;
        if (request.HasTeamId)
        {
            if (!Guid.TryParse(request.TeamId, out var parsedTeamId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid team_id."));
            teamId = parsedTeamId;
        }

        try
        {
            var members = await memberService.ListAsync(
                organizationId,
                actorUserId,
                roleId,
                teamId,
                context.CancellationToken);

            var response = new ListMembersResponse();
            response.Members.AddRange(members.Select(m =>
            {
                var member = new Member
                {
                    UserId = m.UserId.ToString(),
                    JoinedAt = m.JoinedAt.ToString("O")
                };
                member.TeamIds.AddRange(m.TeamIds.Select(id => id.ToString()));
                member.Roles.AddRange(m.Roles.Select(r => new Role
                {
                    Id = r.Id.ToString(),
                    Name = r.Name,
                    Scope = r.Scope,
                    IsSystem = r.IsSystem
                }));
                return member;
            }));

            return response;
        }
        catch (OrganizationAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (OrganizationNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    /// <summary>List organization activity feed for an authenticated actor.</summary>
    public override async Task<ListActivityResponse> ListActivity(
        ListActivityRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrganizationId, out var organizationId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid organization_id."));

        if (!Guid.TryParse(request.ActorUserId, out var actorUserId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid actor_user_id."));

        DateTimeOffset? from = null;
        if (request.HasFrom)
        {
            if (!DateTimeOffset.TryParse(request.From, out var parsedFrom))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid from."));
            from = parsedFrom;
        }

        DateTimeOffset? to = null;
        if (request.HasTo)
        {
            if (!DateTimeOffset.TryParse(request.To, out var parsedTo))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid to."));
            to = parsedTo;
        }

        try
        {
            var page = await activityService.ListAsync(
                organizationId,
                actorUserId,
                request.HasType ? request.Type : null,
                request.HasQ ? request.Q : null,
                from,
                to,
                request.Page > 0 ? request.Page : 1,
                request.PageSize > 0 ? request.PageSize : 20,
                context.CancellationToken);

            var response = new ListActivityResponse
            {
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = page.TotalCount
            };

            response.Items.AddRange(page.Items.Select(item =>
            {
                var mapped = new ActivityItem
                {
                    Id = item.Id.ToString(),
                    Type = item.Type,
                    OccurredAt = item.OccurredAt.ToString("O")
                };

                if (item.ActorUserId is Guid actorId)
                    mapped.ActorUserId = actorId.ToString();
                if (item.TargetUserId is Guid targetId)
                    mapped.TargetUserId = targetId.ToString();
                if (!string.IsNullOrWhiteSpace(item.EntityType))
                    mapped.EntityType = item.EntityType;
                if (item.EntityId is Guid entityId)
                    mapped.EntityId = entityId.ToString();
                if (!string.IsNullOrWhiteSpace(item.Details))
                    mapped.Details = item.Details;

                return mapped;
            }));

            return response;
        }
        catch (OrganizationAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (OrganizationNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }
}
