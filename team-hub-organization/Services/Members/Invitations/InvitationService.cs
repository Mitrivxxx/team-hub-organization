using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.Invitations;

public sealed class InvitationService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IActivityRecorder activity) : IInvitationService
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
        return await MapManyAsync(invitations, includeToken: false, cancellationToken);
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

        return invitation is null ? null : await MapOneAsync(invitation, includeToken: false, cancellationToken);
    }

    public async Task<InvitationResponse> CreateAsync(
        Guid organizationId,
        CreateInvitationRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        var orgRoles = await ResolveOrgRolesAsync(organizationId, request.OrgRoleIds, cancellationToken);

        if (orgRoles.Any(r => r.Name == SystemRoleNames.Owner)
            && !await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken))
            throw new OrganizationAccessException("Only owners can invite users as Owner.");

        Guid? teamRoleId = null;
        if (request.TeamId is Guid teamId)
        {
            _ = await db.Teams.AsNoTracking()
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
            TeamRoleId = teamRoleId,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.Add(DefaultExpiry)
        };

        db.Invitations.Add(invitation);
        foreach (var role in orgRoles)
        {
            db.InvitationOrgRoles.Add(new InvitationOrgRole
            {
                InvitationId = invitation.Id,
                RoleId = role.Id
            });
        }

        activity.Record(
            organizationId,
            ActivityTypes.InvitationSent,
            actorUserId,
            entityType: ActivityEntityTypes.Invitation,
            entityId: invitation.Id,
            details: new
            {
                email,
                roles = orgRoles.Select(r => r.Name).ToArray(),
                teamId = request.TeamId
            },
            occurredAt: now);

        await db.SaveChangesAsync(cancellationToken);
        return await MapOneAsync(invitation, includeToken: true, cancellationToken);
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
        activity.Record(
            organizationId,
            ActivityTypes.InvitationExpired,
            actorUserId,
            entityType: ActivityEntityTypes.Invitation,
            entityId: invitation.Id,
            details: new { email = invitation.Email, reason = "cancelled" });
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
        activity.Record(
            organizationId,
            ActivityTypes.InvitationResent,
            actorUserId,
            entityType: ActivityEntityTypes.Invitation,
            entityId: invitation.Id,
            details: new { email = invitation.Email });
        await db.SaveChangesAsync(cancellationToken);
        return await MapOneAsync(invitation, includeToken: true, cancellationToken);
    }

    public async Task<InvitationResponse?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken);
        if (invitation is null)
            return null;

        await ExpireIfNeededAsync(invitation, cancellationToken);
        return await MapOneAsync(invitation, includeToken: false, cancellationToken);
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

        var orgRoleIds = await db.InvitationOrgRoles.AsNoTracking()
            .Where(x => x.InvitationId == invitation.Id)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);

        if (orgRoleIds.Count == 0)
            throw new OrganizationValidationException("Invitation has no organization roles.");

        var now = DateTimeOffset.UtcNow;
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = invitation.OrganizationId,
            UserId = userId,
            JoinedAt = now
        });

        foreach (var roleId in orgRoleIds)
        {
            db.OrganizationMemberRoles.Add(new OrganizationMemberRole
            {
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                RoleId = roleId,
                AssignedAt = now
            });
        }

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

        var roleNames = await db.Roles.AsNoTracking()
            .Where(r => orgRoleIds.Contains(r.Id))
            .Select(r => r.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        activity.Record(
            invitation.OrganizationId,
            ActivityTypes.InvitationAccepted,
            userId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Invitation,
            entityId: invitation.Id,
            details: new { email = invitation.Email, roles = roleNames },
            occurredAt: now);
        activity.Record(
            invitation.OrganizationId,
            ActivityTypes.MemberJoined,
            userId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Member,
            entityId: userId,
            details: new { roles = roleNames, via = "invitation" },
            occurredAt: now);

        await db.SaveChangesAsync(cancellationToken);

        var roles = await db.Roles.AsNoTracking()
            .Where(r => orgRoleIds.Contains(r.Id))
            .Select(r => new RoleSummaryDto
            {
                Id = r.Id,
                Name = r.Name,
                Scope = r.Scope == RoleScope.Org ? "ORG" : "TEAM",
                IsSystem = r.IsSystem
            })
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

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
            Roles = roles,
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
        activity.Record(
            invitation.OrganizationId,
            ActivityTypes.InvitationRejected,
            userId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Invitation,
            entityId: invitation.Id,
            details: new { email = invitation.Email });
        await db.SaveChangesAsync(cancellationToken);
        return await MapOneAsync(invitation, includeToken: false, cancellationToken);
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

        return await MapManyAsync(invitations, includeToken: false, cancellationToken);
    }

    async Task<List<Role>> ResolveOrgRolesAsync(Guid organizationId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinct = roleIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
            throw new OrganizationValidationException("At least one orgRoleId is required.");

        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Scope == RoleScope.Org && distinct.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != distinct.Count)
            throw new OrganizationValidationException("One or more organization roles were not found.");

        return roles;
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
        {
            invitation.Status = InvitationStatus.Expired;
            activity.Record(
                organizationId,
                ActivityTypes.InvitationExpired,
                actorUserId: null,
                entityType: ActivityEntityTypes.Invitation,
                entityId: invitation.Id,
                details: new { email = invitation.Email, reason = "expired" });
        }

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
        {
            invitation.Status = InvitationStatus.Expired;
            activity.Record(
                invitation.OrganizationId,
                ActivityTypes.InvitationExpired,
                actorUserId: null,
                entityType: ActivityEntityTypes.Invitation,
                entityId: invitation.Id,
                details: new { email = invitation.Email, reason = "expired" });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    async Task ExpireIfNeededAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            activity.Record(
                invitation.OrganizationId,
                ActivityTypes.InvitationExpired,
                actorUserId: null,
                entityType: ActivityEntityTypes.Invitation,
                entityId: invitation.Id,
                details: new { email = invitation.Email, reason = "expired" });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    async Task<InvitationResponse> MapOneAsync(Invitation invitation, bool includeToken, CancellationToken cancellationToken)
    {
        var mapped = await MapManyAsync([invitation], includeToken, cancellationToken);
        return mapped[0];
    }

    async Task<IReadOnlyList<InvitationResponse>> MapManyAsync(
        IReadOnlyList<Invitation> invitations,
        bool includeToken,
        CancellationToken cancellationToken)
    {
        var invitationIds = invitations.Select(i => i.Id).ToList();
        var roleRows = await db.InvitationOrgRoles.AsNoTracking()
            .Where(x => invitationIds.Contains(x.InvitationId))
            .ToListAsync(cancellationToken);

        var rolesByInvitation = roleRows
            .GroupBy(x => x.InvitationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.RoleId).ToList());

        return invitations.Select(invitation => new InvitationResponse
        {
            Id = invitation.Id,
            OrganizationId = invitation.OrganizationId,
            TeamId = invitation.TeamId,
            Email = invitation.Email,
            InvitedByUserId = invitation.InvitedByUserId,
            OrgRoleIds = rolesByInvitation.GetValueOrDefault(invitation.Id, []),
            TeamRoleId = invitation.TeamRoleId,
            Status = invitation.Status.ToString().ToUpperInvariant(),
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            Token = includeToken ? invitation.Token : null
        }).ToList();
    }

    static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
