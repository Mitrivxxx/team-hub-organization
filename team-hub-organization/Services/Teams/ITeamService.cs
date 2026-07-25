using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Teams;

public interface ITeamService
{
    Task<IReadOnlyList<TeamResponse>> ListAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<TeamResponse?> GetAsync(Guid organizationId, Guid teamId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<TeamResponse> CreateAsync(Guid organizationId, CreateTeamRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<TeamResponse?> UpdateAsync(Guid organizationId, Guid teamId, UpdateTeamRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid organizationId, Guid teamId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamMemberResponse>> ListMembersAsync(Guid organizationId, Guid teamId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<TeamMemberResponse> AddMemberAsync(Guid organizationId, Guid teamId, AddTeamMemberRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<TeamMemberResponse?> UpdateMemberAsync(Guid organizationId, Guid teamId, Guid userId, UpdateTeamMemberRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveMemberAsync(Guid organizationId, Guid teamId, Guid userId, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ITeamAvatarService
{
    Task<TeamResponse?> UploadAsync(Guid organizationId, Guid teamId, IFormFile file, Guid userId, CancellationToken cancellationToken = default);
    Task<TeamResponse?> DeleteAsync(Guid organizationId, Guid teamId, Guid userId, CancellationToken cancellationToken = default);
}
