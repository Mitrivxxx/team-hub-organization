using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Members.AllMembers;

public interface IMemberService
{
    Task<IReadOnlyList<MemberResponse>> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid? roleId = null,
        Guid? teamId = null,
        CancellationToken cancellationToken = default);
    Task<MemberResponse?> GetAsync(Guid organizationId, Guid userId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<MemberResponse> AddAsync(Guid organizationId, AddMemberRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<MemberResponse?> UpdateAsync(Guid organizationId, Guid userId, UpdateMemberRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid organizationId, Guid userId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamMembershipResponse>> ListTeamsAsync(Guid organizationId, Guid userId, Guid actorUserId, CancellationToken cancellationToken = default);
}
