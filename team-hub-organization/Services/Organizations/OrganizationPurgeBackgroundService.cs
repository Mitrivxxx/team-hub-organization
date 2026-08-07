using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using TeamHub.BlobStorage;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationPurgeBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrganizationLifecycleOptions> lifecycleOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error during organization retention sweep");
            }

            var delay = TimeSpan.FromMinutes(Math.Max(1, lifecycleOptions.Value.PurgeIntervalMinutes));
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var blob = scope.ServiceProvider.GetService<IBlobStorageService>();
        var opts = lifecycleOptions.Value;
        var now = DateTimeOffset.UtcNow;

        await PurgeSoftDeletedOrganizationsAsync(db, blob, now.AddDays(-Math.Max(1, opts.RetentionDays)), cancellationToken);
        await PurgeSoftDeletedTeamsAsync(db, blob, now.AddDays(-Math.Max(1, opts.RetentionDays)), cancellationToken);

        var activityCutoff = now.AddDays(-Math.Max(1, opts.ActivityTtlDays));
        var oldActivities = await db.OrganizationActivities
            .Where(a => a.OccurredAt < activityCutoff)
            .ToListAsync(cancellationToken);
        if (oldActivities.Count > 0)
        {
            db.OrganizationActivities.RemoveRange(oldActivities);
            await db.SaveChangesAsync(cancellationToken);
            Log.Information("Purged {Count} activity rows older than {Cutoff}", oldActivities.Count, activityCutoff);
        }

        await PurgeImportArtifactsAsync(db, blob, now.AddDays(-Math.Max(1, opts.ImportArtifactTtlDays)), cancellationToken);

        var idempotencyCutoff = now.AddHours(-Math.Max(1, opts.IdempotencyTtlHours));
        var oldKeys = await db.IdempotencyRecords
            .Where(r => r.CreatedAt < idempotencyCutoff)
            .ToListAsync(cancellationToken);
        if (oldKeys.Count > 0)
        {
            db.IdempotencyRecords.RemoveRange(oldKeys);
            await db.SaveChangesAsync(cancellationToken);
            Log.Information("Purged {Count} idempotency records older than {Cutoff}", oldKeys.Count, idempotencyCutoff);
        }
    }

    static async Task PurgeSoftDeletedOrganizationsAsync(
        OrganizationDbContext db,
        IBlobStorageService? blob,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var expired = await db.Organizations
            .Where(o => o.DeletedAt != null && o.DeletedAt <= cutoff)
            .Select(o => new { o.Id, o.Name, o.AvatarUrl })
            .ToListAsync(cancellationToken);

        foreach (var org in expired)
        {
            try
            {
                var teamAvatarPaths = await db.Teams.AsNoTracking()
                    .Where(t => t.OrganizationId == org.Id && t.AvatarUrl != null)
                    .Select(t => t.AvatarUrl!)
                    .ToListAsync(cancellationToken);

                var jobBlobPaths = await db.ImportExportJobs.AsNoTracking()
                    .Where(j => j.OrganizationId == org.Id)
                    .Select(j => new { j.SourceBlobPath, j.ResultBlobPath, j.ErrorBlobPath })
                    .ToListAsync(cancellationToken);

                var blobPaths = new HashSet<string>(StringComparer.Ordinal);
                if (!string.IsNullOrWhiteSpace(org.AvatarUrl))
                    blobPaths.Add(org.AvatarUrl);
                foreach (var path in teamAvatarPaths)
                    blobPaths.Add(path);
                foreach (var job in jobBlobPaths)
                {
                    if (!string.IsNullOrWhiteSpace(job.SourceBlobPath))
                        blobPaths.Add(job.SourceBlobPath);
                    if (!string.IsNullOrWhiteSpace(job.ResultBlobPath))
                        blobPaths.Add(job.ResultBlobPath);
                    if (!string.IsNullOrWhiteSpace(job.ErrorBlobPath))
                        blobPaths.Add(job.ErrorBlobPath);
                }

                await DeleteOrganizationGraphAsync(db, org.Id, cancellationToken);

                if (blob is not null)
                {
                    foreach (var path in blobPaths)
                    {
                        try
                        {
                            await blob.DeleteIfExistsAsync(path, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to delete blob {BlobPath} while purging org {OrganizationId}", path, org.Id);
                        }
                    }
                }

                Log.Information("Purged soft-deleted organization {OrganizationId} ({OrganizationName})", org.Id, org.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to purge organization {OrganizationId}", org.Id);
            }
        }
    }

    static async Task DeleteOrganizationGraphAsync(
        OrganizationDbContext db,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var teamIds = await db.Teams.Where(t => t.OrganizationId == organizationId).Select(t => t.Id).ToListAsync(cancellationToken);
        var teamMembers = await db.TeamMembers.Where(tm => teamIds.Contains(tm.TeamId)).ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(teamMembers);
        var teams = await db.Teams.Where(t => t.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.Teams.RemoveRange(teams);

        var invitationIds = await db.Invitations.Where(i => i.OrganizationId == organizationId).Select(i => i.Id).ToListAsync(cancellationToken);
        var invitationOrgRoles = await db.InvitationOrgRoles.Where(x => invitationIds.Contains(x.InvitationId)).ToListAsync(cancellationToken);
        db.InvitationOrgRoles.RemoveRange(invitationOrgRoles);
        var invitations = await db.Invitations.Where(i => i.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.Invitations.RemoveRange(invitations);

        var memberRoles = await db.OrganizationMemberRoles.Where(m => m.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.OrganizationMemberRoles.RemoveRange(memberRoles);
        var members = await db.OrganizationMembers.Where(m => m.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.OrganizationMembers.RemoveRange(members);

        var roleIds = await db.Roles.Where(r => r.OrganizationId == organizationId).Select(r => r.Id).ToListAsync(cancellationToken);
        var permissionIds = await db.Permissions.Where(p => p.OrganizationId == organizationId).Select(p => p.Id).ToListAsync(cancellationToken);
        var rolePermissions = await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId) || permissionIds.Contains(rp.PermissionId))
            .ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(rolePermissions);
        var roles = await db.Roles.Where(r => r.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.Roles.RemoveRange(roles);
        var permissions = await db.Permissions.Where(p => p.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.Permissions.RemoveRange(permissions);

        var activities = await db.OrganizationActivities.Where(a => a.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.OrganizationActivities.RemoveRange(activities);
        var audits = await db.OrganizationAuditEvents.Where(a => a.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.OrganizationAuditEvents.RemoveRange(audits);
        var jobs = await db.ImportExportJobs.Where(j => j.OrganizationId == organizationId).ToListAsync(cancellationToken);
        db.ImportExportJobs.RemoveRange(jobs);

        var entity = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (entity is not null)
            db.Organizations.Remove(entity);

        await db.SaveChangesAsync(cancellationToken);
    }

    static async Task PurgeSoftDeletedTeamsAsync(
        OrganizationDbContext db,
        IBlobStorageService? blob,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var teams = await db.Teams
            .Where(t => t.DeletedAt != null && t.DeletedAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            if (blob is not null && !string.IsNullOrWhiteSpace(team.AvatarUrl))
            {
                try { await blob.DeleteIfExistsAsync(team.AvatarUrl, cancellationToken); }
                catch (Exception ex) { Log.Warning(ex, "Failed to delete team avatar {Path}", team.AvatarUrl); }
            }

            db.Teams.Remove(team);
            Log.Information("Hard-purged soft-deleted team {TeamId}", team.Id);
        }

        if (teams.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    static async Task PurgeImportArtifactsAsync(
        OrganizationDbContext db,
        IBlobStorageService? blob,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var jobs = await db.ImportExportJobs
            .Where(j => j.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            if (blob is not null)
            {
                foreach (var path in new[] { job.SourceBlobPath, job.ResultBlobPath, job.ErrorBlobPath })
                {
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    try { await blob.DeleteIfExistsAsync(path, cancellationToken); }
                    catch (Exception ex) { Log.Warning(ex, "Failed to delete import/export blob {Path}", path); }
                }
            }

            db.ImportExportJobs.Remove(job);
        }

        if (jobs.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            Log.Information("Purged {Count} import/export jobs older than {Cutoff}", jobs.Count, cutoff);
        }
    }
}
