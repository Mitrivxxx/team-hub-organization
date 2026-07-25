using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.Invitations;

public sealed class InvitationService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz) : IInvitationService
{
    static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<InvitationResponse>> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        InvitationStatus? status,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);
        await ExpirePendingAsync(organizationId, cancellationToken);

        var query = db.Invitations.AsNoTracking().Where(i => i.OrganizationId == organizationId);
        if (status is not null)
            query = query.Where(i => i.Status == status);

        var invitations = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        return invitations.Select(i => ToResponse(i, includeToken: false)).ToList();
    }

    public async Task<InvitationResponse?> GetAsync(
        Guid organizationId,
        Guid invitationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);
        await ExpirePendingAsync(organizationId, cancellationToken);

        var invitation = await db.Invitations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == organizationId, cancellationToken);

        return invitation is null ? null : ToResponse(invitation, includeToken: false);
    }

    public async Task<InvitationResponse> CreateAsync(
        Guid organizationId,
        CreateInvitationRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        var orgRole = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.OrgRoleId && r.OrganizationId == organizationId && r.Scope == RoleScope.Org,
                cancellationToken)
            ?? throw new OrganizationValidationException("Organization role was not found.");

        if (orgRole.Name == SystemRoleNames.Owner
            && !await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken))
            throw new OrganizationAccessException("Only owners can invite users as Owner.");

        Guid? teamRoleId = null;
        if (request.TeamId is Guid teamId)
        {
            var team = await db.Teams.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken)
                ?? throw new OrganizationValidationException("Team was not found.");

            if (request.TeamRoleId is not Guid teamRoleGuid)
                throw new OrganizationValidationException("TeamRoleId is required when TeamId is set.");

            var teamRole = await db.Roles.AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.Id == teamRoleGuid && r.OrganizationId == organizationId && r.Scope == RoleScope.Team,
                    cancellationToken)
                ?? throw new OrganizationValidationException("Team role was not found.");

            teamRoleId = teamRole.Id;
            _ = team;
        }
        else if (request.TeamRoleId is not null)
        {
            throw new OrganizationValidationException("TeamRoleId requires TeamId.");
        }

        var pendingExists = await db.Invitations.AnyAsync(
            i => i.OrganizationId == organizationId
                 && i.Email == email
                 && i.Status == InvitationStatus.Pending
                 && i.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);

        if (pendingExists)
            throw new OrganizationConflictException("A pending invitation already exists for this email.");

        var now = DateTimeOffset.UtcNow;
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TeamId = request.TeamId,
            Email = email,
            InvitedByUserId = actorUserId,
            Token = GenerateToken(),
            OrgRoleId = orgRole.Id,
            TeamRoleId = teamRoleId,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.Add(DefaultExpiry)
        };

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(invitation, includeToken: true);
    }

    public async Task CancelAsync(
        Guid organizationId,
        Guid invitationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == organizationId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Invitation was not found.");

        if (invitation.Status != InvitationStatus.Pending)
            throw new OrganizationConflictException("Only pending invitations can be cancelled.");

        invitation.Status = InvitationStatus.Expired;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitationResponse> ResendAsync(
        Guid organizationId,
        Guid invitationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == organizationId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Invitation was not found.");

        if (invitation.Status is not InvitationStatus.Pending and not InvitationStatus.Expired)
            throw new OrganizationConflictException("Invitation cannot be resent.");

        invitation.Token = GenerateToken();
        invitation.Status = InvitationStatus.Pending;
        invitation.ExpiresAt = DateTimeOffset.UtcNow.Add(DefaultExpiry);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(invitation, includeToken: true);
    }

    public async Task<InvitationResponse?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken);
        if (invitation is null)
            return null;

        await ExpireIfNeededAsync(invitation, cancellationToken);
        return ToResponse(invitation, includeToken: false);
    }

    public async Task<MemberResponse> AcceptAsync(string token, Guid userId, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken)
            ?? throw new OrganizationNotFoundException("Invitation was not found.");

        await ExpireIfNeededAsync(invitation, cancellationToken);

        if (invitation.Status != InvitationStatus.Pending)
            throw new OrganizationConflictException("Invitation is not pending.");

        if (await db.OrganizationMembers.AnyAsync(
                m => m.OrganizationId == invitation.OrganizationId && m.UserId == userId,
                cancellationToken))
            throw new OrganizationConflictException("User is already a member of this organization.");

        var now = DateTimeOffset.UtcNow;
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = invitation.OrganizationId,
            UserId = userId,
            RoleId = invitation.OrgRoleId,
            JoinedAt = now
        });

        if (invitation.TeamId is Guid teamId && invitation.TeamRoleId is Guid teamRoleId)
        {
            var teamExists = await db.Teams.AnyAsync(
                t => t.Id == teamId && t.OrganizationId == invitation.OrganizationId && t.DeletedAt == null,
                cancellationToken);

            if (teamExists && !await db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken))
            {
                db.TeamMembers.Add(new TeamMember
                {
                    TeamId = teamId,
                    UserId = userId,
                    RoleId = teamRoleId,
                    JoinedAt = now
                });
            }
        }

        invitation.Status = InvitationStatus.Accepted;
        await db.SaveChangesAsync(cancellationToken);

        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Id == invitation.OrgRoleId, cancellationToken);
        var teamIds = await db.TeamMembers.AsNoTracking()
            .Where(tm => tm.UserId == userId)
            .Join(
                db.Teams.AsNoTracking().Where(t => t.OrganizationId == invitation.OrganizationId && t.DeletedAt == null),
                tm => tm.TeamId,
                t => t.Id,
                (tm, _) => tm.TeamId)
            .ToListAsync(cancellationToken);

        return new MemberResponse
        {
            UserId = userId,
            RoleId = role.Id,
            RoleName = role.Name,
            JoinedAt = now,
            TeamIds = teamIds
        };
    }

    public async Task<InvitationResponse> RejectAsync(string token, Guid userId, CancellationToken cancellationToken = default)
    {
        _ = userId;
        var invitation = await FindByTokenAsync(token, cancellationToken)
            ?? throw new OrganizationNotFoundException("Invitation was not found.");

        await ExpireIfNeededAsync(invitation, cancellationToken);

        if (invitation.Status != InvitationStatus.Pending)
            throw new OrganizationConflictException("Invitation is not pending.");

        invitation.Status = InvitationStatus.Rejected;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(invitation, includeToken: false);
    }

    public async Task<IReadOnlyList<InvitationResponse>> ListMineAsync(
        string? email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return [];

        var normalized = email.Trim().ToLowerInvariant();
        await ExpirePendingForEmailAsync(normalized, cancellationToken);

        var invitations = await db.Invitations.AsNoTracking()
            .Where(i => i.Email == normalized && i.Status == InvitationStatus.Pending && i.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invitations.Select(i => ToResponse(i, includeToken: false)).ToList();
    }

    async Task<Invitation?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        await db.Invitations.FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

    async Task ExpirePendingAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await db.Invitations
            .Where(i => i.OrganizationId == organizationId
                        && i.Status == InvitationStatus.Pending
                        && i.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        foreach (var invitation in expired)
            invitation.Status = InvitationStatus.Expired;

        await db.SaveChangesAsync(cancellationToken);
    }

    async Task ExpirePendingForEmailAsync(string email, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await db.Invitations
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        foreach (var invitation in expired)
            invitation.Status = InvitationStatus.Expired;

        await db.SaveChangesAsync(cancellationToken);
    }

    async Task ExpireIfNeededAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    static InvitationResponse ToResponse(Invitation invitation, bool includeToken) => new()
    {
        Id = invitation.Id,
        OrganizationId = invitation.OrganizationId,
        TeamId = invitation.TeamId,
        Email = invitation.Email,
        InvitedByUserId = invitation.InvitedByUserId,
        OrgRoleId = invitation.OrgRoleId,
        TeamRoleId = invitation.TeamRoleId,
        Status = invitation.Status.ToString().ToUpperInvariant(),
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        Token = includeToken ? invitation.Token : null
    };

    static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
