using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Auth;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.ImportExport;

public interface IImportExportJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken);
}

public sealed class ImportExportJobProcessor(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IAuthUserResolveClient authUsers,
    IActivityRecorder activity,
    IServiceProvider serviceProvider) : IImportExportJobProcessor
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.ImportExportJobs
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            Log.Warning("Import/export job {JobId} was not found", jobId);
            return;
        }

        if (job.Status is not ImportExportJobStatus.Queued)
            return;

        job.Status = ImportExportJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            if (job.Type == ImportExportJobType.Import)
                await ProcessImportAsync(job, cancellationToken);
            else
                await ProcessExportAsync(job, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Import/export job {JobId} failed", jobId);
            job.Status = ImportExportJobStatus.Failed;
            job.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    async Task ProcessImportAsync(ImportExportJob job, CancellationToken cancellationToken)
    {
        var blobStorage = BlobStorage ?? throw new InvalidOperationException("Blob storage is not configured.");
        if (string.IsNullOrWhiteSpace(job.SourceBlobPath))
            throw new InvalidOperationException("Import job has no source blob.");

        await using var sourceStream = await blobStorage.OpenReadAsync(job.SourceBlobPath, cancellationToken);
        var rows = ImportCsvParser.Parse(sourceStream);

        var emails = rows.Select(r => r.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Cast<string>().ToList();
        var usernames = rows.Select(r => r.Username).Where(u => !string.IsNullOrWhiteSpace(u)).Cast<string>().ToList();
        var resolved = await authUsers.ResolveUsersAsync(emails, usernames, cancellationToken);
        var byEmail = resolved.Users
            .GroupBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var byUsername = resolved.Users
            .GroupBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var orgRoles = await db.Roles
            .Where(r => r.OrganizationId == job.OrganizationId && r.Scope == RoleScope.Org)
            .ToListAsync(cancellationToken);
        var orgRolesByName = orgRoles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var teams = await db.Teams
            .Where(t => t.OrganizationId == job.OrganizationId && t.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var teamsByName = teams.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var teamRoles = await db.Roles
            .Where(r => r.OrganizationId == job.OrganizationId && r.Scope == RoleScope.Team)
            .ToListAsync(cancellationToken);
        var teamRolesByName = teamRoles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var actorIsOwner = await authz.IsOwnerAsync(job.OrganizationId, job.CreatedByUserId, cancellationToken);

        var errors = new List<(ImportCsvRow Row, string Code, string Message)>();
        var success = 0;

        foreach (var row in rows)
        {
            try
            {
                var rowErrors = ValidateAndResolve(
                    row,
                    byEmail,
                    byUsername,
                    orgRolesByName,
                    teamsByName,
                    teamRolesByName,
                    actorIsOwner,
                    out var userId,
                    out var roleList,
                    out var team,
                    out var teamRole);

                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors.Select(e => (row, e.Code, e.Message)));
                    continue;
                }

                await UpsertMemberAsync(
                    job.OrganizationId,
                    job.CreatedByUserId,
                    userId,
                    roleList!,
                    team,
                    teamRole,
                    row.JobTitle,
                    cancellationToken);

                success++;
            }
            catch (Exception ex)
            {
                errors.Add((row, ImportErrorCodes.ValidationError, ex.Message));
            }
        }

        job.TotalRows = rows.Count;
        job.SuccessCount = success;
        job.ErrorCount = errors.Count;

        if (errors.Count > 0)
        {
            var errorCsv = ImportCsvParser.BuildErrorCsv(errors);
            var errorPath = BlobStoragePaths.ImportExportErrors(job.OrganizationId, job.Id);
            await using var errorStream = new MemoryStream(Encoding.UTF8.GetBytes(errorCsv));
            await blobStorage.UploadAsync(errorPath, errorStream, "text/csv", cancellationToken);
            job.ErrorBlobPath = errorPath;
        }

        job.Status = errors.Count == 0
            ? ImportExportJobStatus.Completed
            : ImportExportJobStatus.CompletedWithErrors;
        job.CompletedAt = DateTimeOffset.UtcNow;

        activity.Record(
            job.OrganizationId,
            ActivityTypes.ImportCompleted,
            job.CreatedByUserId,
            entityType: ActivityEntityTypes.ImportExport,
            entityId: job.Id,
            details: new { job.SuccessCount, job.ErrorCount, job.TotalRows });

        await db.SaveChangesAsync(cancellationToken);
    }

    async Task ProcessExportAsync(ImportExportJob job, CancellationToken cancellationToken)
    {
        var blobStorage = BlobStorage ?? throw new InvalidOperationException("Blob storage is not configured.");

        var datasets = ParseDatasets(job.OptionsJson);
        var payload = await BuildExportPayloadAsync(job.OrganizationId, datasets, cancellationToken);

        string content;
        string extension;
        string contentType;

        if (job.Format == ImportExportFormat.Json)
        {
            content = JsonSerializer.Serialize(payload, JsonOptions);
            extension = "json";
            contentType = "application/json";
        }
        else
        {
            content = BuildMembersCompatibleCsv(payload);
            extension = "csv";
            contentType = "text/csv";
        }

        job.TotalRows = payload.Members.Count
                        + payload.Teams.Count
                        + payload.Roles.Count
                        + payload.Permissions.Count
                        + (payload.Organization is null ? 0 : 1);
        job.SuccessCount = job.TotalRows;

        var org = await db.Organizations.AsNoTracking()
            .FirstAsync(o => o.Id == job.OrganizationId, cancellationToken);
        var fileBaseName = !string.IsNullOrWhiteSpace(org.Slug) ? org.Slug : org.Name;
        var downloadFileName = $"{BlobStoragePaths.SanitizeFileBaseName(fileBaseName)}.{extension}";
        var resultPath = BlobStoragePaths.ImportExportResult(job.OrganizationId, job.Id, fileBaseName, extension);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blobStorage.UploadAsync(resultPath, stream, contentType, cancellationToken, downloadFileName);

        job.ResultBlobPath = resultPath;
        job.ErrorCount = 0;
        job.Status = ImportExportJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;

        activity.Record(
            job.OrganizationId,
            ActivityTypes.ExportCompleted,
            job.CreatedByUserId,
            entityType: ActivityEntityTypes.ImportExport,
            entityId: job.Id,
            details: new { datasets, format = job.Format.ToString().ToLowerInvariant(), job.TotalRows });

        await db.SaveChangesAsync(cancellationToken);
    }

    async Task UpsertMemberAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid userId,
        IReadOnlyList<Role> roles,
        Team? team,
        Role? teamRole,
        string? jobTitle,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (existing is null)
        {
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = userId,
                JoinedAt = now
            });

            foreach (var role in roles)
            {
                db.OrganizationMemberRoles.Add(new OrganizationMemberRole
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    RoleId = role.Id,
                    AssignedAt = now
                });
            }

            activity.Record(
                organizationId,
                ActivityTypes.MemberJoined,
                actorUserId,
                targetUserId: userId,
                entityType: ActivityEntityTypes.Member,
                entityId: userId,
                details: new { roles = roles.Select(r => r.Name).ToArray(), source = "import" },
                occurredAt: now);
        }
        else
        {
            var current = await db.OrganizationMemberRoles
                .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
                .ToListAsync(cancellationToken);

            var currentOwner = await db.OrganizationMemberRoles
                .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
                .Join(db.Roles, m => m.RoleId, r => r.Id, (_, r) => r)
                .AnyAsync(r => r.Name == SystemRoleNames.Owner && r.Scope == RoleScope.Org, cancellationToken);

            var newHasOwner = roles.Any(r => r.Name == SystemRoleNames.Owner && r.Scope == RoleScope.Org);
            if (currentOwner && !newHasOwner)
            {
                var ownerCount = await db.OrganizationMemberRoles
                    .Join(db.Roles, m => m.RoleId, r => r.Id, (m, r) => new { m, r })
                    .CountAsync(
                        x => x.m.OrganizationId == organizationId
                             && x.r.Name == SystemRoleNames.Owner
                             && x.r.Scope == RoleScope.Org,
                        cancellationToken);
                if (ownerCount <= 1)
                    throw new InvalidOperationException("Cannot demote the last organization owner.");
            }

            db.OrganizationMemberRoles.RemoveRange(current);
            foreach (var role in roles)
            {
                db.OrganizationMemberRoles.Add(new OrganizationMemberRole
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    RoleId = role.Id,
                    AssignedAt = now
                });
            }

            activity.Record(
                organizationId,
                ActivityTypes.MemberRolesChanged,
                actorUserId,
                targetUserId: userId,
                entityType: ActivityEntityTypes.Member,
                entityId: userId,
                details: new { toRoles = roles.Select(r => r.Name).ToArray(), source = "import" },
                occurredAt: now);
        }

        if (team is not null && teamRole is not null)
        {
            var teamMember = await db.TeamMembers
                .FirstOrDefaultAsync(tm => tm.TeamId == team.Id && tm.UserId == userId, cancellationToken);

            if (teamMember is null)
            {
                db.TeamMembers.Add(new TeamMember
                {
                    TeamId = team.Id,
                    UserId = userId,
                    RoleId = teamRole.Id,
                    JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim(),
                    JoinedAt = now
                });
                activity.Record(
                    organizationId,
                    ActivityTypes.TeamMemberAdded,
                    actorUserId,
                    targetUserId: userId,
                    entityType: ActivityEntityTypes.Team,
                    entityId: team.Id,
                    details: new { teamName = team.Name, roleName = teamRole.Name, source = "import" });
            }
            else
            {
                teamMember.RoleId = teamRole.Id;
                if (jobTitle is not null)
                    teamMember.JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
                activity.Record(
                    organizationId,
                    ActivityTypes.TeamMemberUpdated,
                    actorUserId,
                    targetUserId: userId,
                    entityType: ActivityEntityTypes.Team,
                    entityId: team.Id,
                    details: new { teamName = team.Name, roleName = teamRole.Name, source = "import" });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    static List<ImportRowError> ValidateAndResolve(
        ImportCsvRow row,
        Dictionary<string, List<AuthUserProfile>> byEmail,
        Dictionary<string, List<AuthUserProfile>> byUsername,
        Dictionary<string, Role> orgRolesByName,
        Dictionary<string, Team> teamsByName,
        Dictionary<string, Role> teamRolesByName,
        bool actorIsOwner,
        out Guid userId,
        out List<Role>? roleList,
        out Team? team,
        out Role? teamRole)
    {
        userId = Guid.Empty;
        roleList = null;
        team = null;
        teamRole = null;
        var errors = new List<ImportRowError>();

        if (string.IsNullOrWhiteSpace(row.Email) && string.IsNullOrWhiteSpace(row.Username))
            errors.Add(new ImportRowError { Code = ImportErrorCodes.ValidationError, Message = "Either email or username is required." });

        if (string.IsNullOrWhiteSpace(row.OrgRoles))
            errors.Add(new ImportRowError { Code = ImportErrorCodes.ValidationError, Message = "org_roles is required." });

        var emailMatches = !string.IsNullOrWhiteSpace(row.Email) && byEmail.TryGetValue(row.Email, out var em) ? em : [];
        var usernameMatches = !string.IsNullOrWhiteSpace(row.Username) && byUsername.TryGetValue(row.Username, out var um) ? um : [];

        if (!string.IsNullOrWhiteSpace(row.Email) || !string.IsNullOrWhiteSpace(row.Username))
        {
            var candidates = emailMatches.Concat(usernameMatches).GroupBy(u => u.Id).Select(g => g.First()).ToList();
            if (candidates.Count == 0)
                errors.Add(new ImportRowError { Code = ImportErrorCodes.UserNotFound, Message = "User does not exist." });
            else if (candidates.Count > 1
                     || (!string.IsNullOrWhiteSpace(row.Email) && !string.IsNullOrWhiteSpace(row.Username)
                         && emailMatches.Count > 0 && usernameMatches.Count > 0
                         && emailMatches[0].Id != usernameMatches[0].Id))
                errors.Add(new ImportRowError { Code = ImportErrorCodes.AmbiguousIdentity, Message = "Email and username resolve to different users." });
            else
                userId = candidates[0].Id;
        }

        var roles = new List<Role>();
        foreach (var roleName in ImportExportService.SplitRoles(row.OrgRoles))
        {
            if (!orgRolesByName.TryGetValue(roleName, out var role))
                errors.Add(new ImportRowError { Code = ImportErrorCodes.InvalidOrgRole, Message = $"Organization role '{roleName}' was not found." });
            else if (role.Name == SystemRoleNames.Owner && !actorIsOwner)
                errors.Add(new ImportRowError { Code = ImportErrorCodes.OwnerAssignmentForbidden, Message = "Only owners can assign the Owner role." });
            else
                roles.Add(role);
        }

        if (roles.Count > 0)
            roleList = roles;

        if (!string.IsNullOrWhiteSpace(row.Team))
        {
            if (!teamsByName.TryGetValue(row.Team, out team))
                errors.Add(new ImportRowError { Code = ImportErrorCodes.InvalidTeam, Message = $"Team '{row.Team}' was not found." });

            var teamRoleName = string.IsNullOrWhiteSpace(row.TeamRole) ? SystemRoleNames.Member : row.TeamRole;
            if (!teamRolesByName.TryGetValue(teamRoleName, out teamRole))
                errors.Add(new ImportRowError { Code = ImportErrorCodes.InvalidTeamRole, Message = $"Team role '{teamRoleName}' was not found." });
        }
        else if (!string.IsNullOrWhiteSpace(row.TeamRole))
        {
            errors.Add(new ImportRowError { Code = ImportErrorCodes.ValidationError, Message = "team_role requires team." });
        }

        return errors;
    }

    async Task<ExportDocument> BuildExportPayloadAsync(
        Guid organizationId,
        IReadOnlyList<string> datasets,
        CancellationToken cancellationToken)
    {
        var payload = new ExportDocument();

        if (datasets.Contains("organization"))
        {
            var org = await db.Organizations.AsNoTracking()
                .FirstAsync(o => o.Id == organizationId, cancellationToken);
            payload.Organization = new ExportOrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Slug = org.Slug,
                Description = org.Description,
                Nip = org.Nip,
                Email = org.Email,
                Country = org.Address?.Country,
                City = org.Address?.City,
                PostalCode = org.Address?.PostalCode,
                CreatedAt = org.CreatedAt
            };
        }

        if (datasets.Contains("permissions"))
        {
            payload.Permissions = await db.Permissions.AsNoTracking()
                .Where(p => p.OrganizationId == organizationId)
                .OrderBy(p => p.Code)
                .Select(p => new ExportPermissionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Description = p.Description,
                    IsSystem = p.IsSystem
                })
                .ToListAsync(cancellationToken);
        }

        if (datasets.Contains("roles"))
        {
            var roles = await db.Roles.AsNoTracking()
                .Where(r => r.OrganizationId == organizationId)
                .OrderBy(r => r.Scope).ThenBy(r => r.Name)
                .ToListAsync(cancellationToken);

            var roleIds = roles.Select(r => r.Id).ToList();
            var rolePerms = await db.RolePermissions.AsNoTracking()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Join(db.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.Id, (rp, p) => new { rp.RoleId, p.Code })
                .ToListAsync(cancellationToken);

            var permsByRole = rolePerms.GroupBy(x => x.RoleId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Code).OrderBy(c => c).ToList());

            payload.Roles = roles.Select(r => new ExportRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Scope = r.Scope == RoleScope.Org ? "ORG" : "TEAM",
                Description = r.Description,
                IsSystem = r.IsSystem,
                PermissionCodes = permsByRole.GetValueOrDefault(r.Id, [])
            }).ToList();
        }

        if (datasets.Contains("teams"))
        {
            var teams = await db.Teams.AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.DeletedAt == null)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken);

            var teamIds = teams.Select(t => t.Id).ToList();
            var members = await db.TeamMembers.AsNoTracking()
                .Where(tm => teamIds.Contains(tm.TeamId))
                .Join(db.Roles.AsNoTracking(), tm => tm.RoleId, r => r.Id, (tm, r) => new { tm, RoleName = r.Name })
                .ToListAsync(cancellationToken);

            var membersByTeam = members.GroupBy(x => x.tm.TeamId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ExportTeamMemberDto>)g.Select(x => new ExportTeamMemberDto
                    {
                        UserId = x.tm.UserId,
                        RoleName = x.RoleName,
                        JobTitle = x.tm.JobTitle
                    }).ToList());

            payload.Teams = teams.Select(t => new ExportTeamDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Members = membersByTeam.GetValueOrDefault(t.Id, [])
            }).ToList();
        }

        if (datasets.Contains("members"))
        {
            var members = await db.OrganizationMembers.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId)
                .OrderBy(m => m.JoinedAt)
                .ToListAsync(cancellationToken);

            var userIds = members.Select(m => m.UserId).ToList();
            var profiles = await authUsers.GetUsersByIdsAsync(userIds, cancellationToken);

            var roleRows = await db.OrganizationMemberRoles.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId)
                .Join(db.Roles.AsNoTracking(), m => m.RoleId, r => r.Id, (m, r) => new { m.UserId, r.Name })
                .ToListAsync(cancellationToken);

            var rolesByUser = roleRows.GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).OrderBy(n => n).ToList());

            var teamRows = await db.TeamMembers.AsNoTracking()
                .Join(
                    db.Teams.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.DeletedAt == null),
                    tm => tm.TeamId,
                    t => t.Id,
                    (tm, t) => new { tm, t })
                .Join(db.Roles.AsNoTracking(), x => x.tm.RoleId, r => r.Id, (x, r) => new
                {
                    x.tm.UserId,
                    TeamName = x.t.Name,
                    RoleName = r.Name,
                    x.tm.JobTitle
                })
                .ToListAsync(cancellationToken);

            var teamsByUser = teamRows.GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ExportMemberTeamDto>)g.Select(x => new ExportMemberTeamDto
                    {
                        TeamName = x.TeamName,
                        RoleName = x.RoleName,
                        JobTitle = x.JobTitle
                    }).ToList());

            payload.Members = members.Select(m =>
            {
                profiles.TryGetValue(m.UserId, out var profile);
                return new ExportMemberDto
                {
                    UserId = m.UserId,
                    Email = profile?.Email,
                    Username = profile?.Username,
                    Name = profile?.Name,
                    Surname = profile?.Surname,
                    OrgRoles = rolesByUser.GetValueOrDefault(m.UserId, []),
                    Teams = teamsByUser.GetValueOrDefault(m.UserId, []),
                    JoinedAt = m.JoinedAt
                };
            }).ToList();
        }

        return payload;
    }

    /// <summary>
    /// CSV export: members in import-compatible columns when present; other datasets appended as JSON lines sections.
    /// </summary>
    static string BuildMembersCompatibleCsv(ExportDocument payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine("email,username,org_roles,team,team_role,job_title");

        foreach (var m in payload.Members)
        {
            var orgRoles = string.Join(';', m.OrgRoles);
            if (m.Teams.Count == 0)
            {
                sb.AppendLine($"{Esc(m.Email)},{Esc(m.Username)},{Esc(orgRoles)},,,");
            }
            else
            {
                foreach (var t in m.Teams)
                    sb.AppendLine($"{Esc(m.Email)},{Esc(m.Username)},{Esc(orgRoles)},{Esc(t.TeamName)},{Esc(t.RoleName)},{Esc(t.JobTitle)}");
            }
        }

        if (payload.Organization is not null
            || payload.Permissions.Count > 0
            || payload.Roles.Count > 0
            || payload.Teams.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# extended_json");
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                payload.Organization,
                payload.Permissions,
                payload.Roles,
                payload.Teams
            }, JsonOptions));
        }

        return sb.ToString();
    }

    static string Esc(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    static IReadOnlyList<string> ParseDatasets(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return ["members", "teams", "roles", "permissions", "organization"];

        try
        {
            using var doc = JsonDocument.Parse(optionsJson);
            if (doc.RootElement.TryGetProperty("datasets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .Select(s => s.ToLowerInvariant())
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return ["members", "teams", "roles", "permissions", "organization"];
    }

    sealed class ExportDocument
    {
        public ExportOrganizationDto? Organization { get; set; }
        public List<ExportPermissionDto> Permissions { get; set; } = [];
        public List<ExportRoleDto> Roles { get; set; } = [];
        public List<ExportTeamDto> Teams { get; set; } = [];
        public List<ExportMemberDto> Members { get; set; } = [];
    }

    sealed class ExportOrganizationDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public string? Nip { get; set; }
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    sealed class ExportPermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
    }

    sealed class ExportRoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Scope { get; set; } = "";
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public IReadOnlyList<string> PermissionCodes { get; set; } = [];
    }

    sealed class ExportTeamDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public IReadOnlyList<ExportTeamMemberDto> Members { get; set; } = [];
    }

    sealed class ExportTeamMemberDto
    {
        public Guid UserId { get; set; }
        public string RoleName { get; set; } = "";
        public string? JobTitle { get; set; }
    }

    sealed class ExportMemberDto
    {
        public Guid UserId { get; set; }
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public IReadOnlyList<string> OrgRoles { get; set; } = [];
        public IReadOnlyList<ExportMemberTeamDto> Teams { get; set; } = [];
        public DateTimeOffset JoinedAt { get; set; }
    }

    sealed class ExportMemberTeamDto
    {
        public string TeamName { get; set; } = "";
        public string RoleName { get; set; } = "";
        public string? JobTitle { get; set; }
    }
}
