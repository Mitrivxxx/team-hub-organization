using Grpc.Core;
using TeamHub.GrpcContracts.Organization.V1;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Grpc;

public sealed class OrganizationMemberGrpcService(IMemberService memberService)
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
}
