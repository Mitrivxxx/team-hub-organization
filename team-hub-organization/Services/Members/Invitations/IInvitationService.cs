using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Members.Invitations;

public interface IInvitationService
{
    Task<IReadOnlyList<InvitationResponse>> ListAsync(Guid organizationId, Guid actorUserId, InvitationStatus? status, CancellationToken cancellationToken = default);
    Task<InvitationResponse?> GetAsync(Guid organizationId, Guid invitationId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<InvitationResponse> CreateAsync(Guid organizationId, CreateInvitationRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid organizationId, Guid invitationId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<InvitationResponse> ResendAsync(Guid organizationId, Guid invitationId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<InvitationResponse?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<MemberResponse> AcceptAsync(string token, Guid userId, CancellationToken cancellationToken = default);
    Task<InvitationResponse> RejectAsync(string token, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvitationResponse>> ListMineAsync(string? email, CancellationToken cancellationToken = default);
}
