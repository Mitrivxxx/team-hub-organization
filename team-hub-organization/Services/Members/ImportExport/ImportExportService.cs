using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Auth;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.ImportExport;

public interface IImportExportService
{
    Task<ImportPreviewResponse> PreviewImportAsync(
        Guid organizationId,
        IFormFile file,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<ImportExportJobAcceptedResponse> StartImportAsync(
        Guid organizationId,
        IFormFile file,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<ImportExportJobAcceptedResponse> StartExportAsync(
        Guid organizationId,
        CreateExportRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportExportJobResponse>> ListJobsAsync(
        Guid organizationId,
        Guid actorUserId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ImportExportJobResponse?> GetJobAsync(
        Guid organizationId,
        Guid jobId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<ImportExportDownloadResponse?> GetDownloadAsync(
        Guid organizationId,
        Guid jobId,
        string artifact,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class ImportExportService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IAuthUserResolveClient authUsers,
    IImportExportJobQueue queue,
    IServiceProvider serviceProvider) : IImportExportService
{
    static readonly HashSet<string> AllowedDatasets = new(StringComparer.OrdinalIgnoreCase)
    {
        "members", "teams", "roles", "permissions", "organization"
    };

    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();

    public async Task<ImportPreviewResponse> PreviewImportAsync(
        Guid organizationId,
        IFormFile file,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);
        await EnsureOrgExistsAsync(organizationId, cancellationToken);

        var rows = await ParseFileAsync(file, cancellationToken);
        var validated = await ValidateRowsAsync(organizationId, actorUserId, rows, cancellationToken);

        return new ImportPreviewResponse
        {
            TotalRows = validated.Count,
            ValidCount = validated.Count(r => r.Status == "valid"),
            ErrorCount = validated.Count(r => r.Status == "error"),
            Rows = validated
        };
    }

    public async Task<ImportExportJobAcceptedResponse> StartImportAsync(
        Guid organizationId,
        IFormFile file,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);
        await EnsureOrgExistsAsync(organizationId, cancellationToken);

        var blobStorage = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();

        // Validate parse early so we fail fast before queuing.
        await using var parseStream = file.OpenReadStream();
        var rows = ImportCsvParser.Parse(parseStream);

        var jobId = Guid.NewGuid();
        var blobPath = BlobStoragePaths.ImportExportSource(organizationId, jobId, "csv");

        await using (var uploadStream = file.OpenReadStream())
        {
            await blobStorage.UploadAsync(blobPath, uploadStream, "text/csv", cancellationToken);
        }

        var job = new ImportExportJob
        {
            Id = jobId,
            OrganizationId = organizationId,
            Type = ImportExportJobType.Import,
            Status = ImportExportJobStatus.Queued,
            Format = ImportExportFormat.Csv,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            TotalRows = rows.Count,
            SourceBlobPath = blobPath
        };

        db.ImportExportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(jobId, cancellationToken);

        return new ImportExportJobAcceptedResponse { JobId = jobId };
    }

    public async Task<ImportExportJobAcceptedResponse> StartExportAsync(
        Guid organizationId,
        CreateExportRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);
        await EnsureOrgExistsAsync(organizationId, cancellationToken);

        _ = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();

        var format = ParseFormat(request.Format);
        var datasets = (request.Datasets ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => AllowedDatasets.Contains(d))
            .Distinct()
            .ToList();

        if (datasets.Count == 0)
            datasets = ["members", "teams", "roles", "permissions", "organization"];

        var jobId = Guid.NewGuid();
        var job = new ImportExportJob
        {
            Id = jobId,
            OrganizationId = organizationId,
            Type = ImportExportJobType.Export,
            Status = ImportExportJobStatus.Queued,
            Format = format,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            OptionsJson = JsonSerializer.Serialize(new { datasets })
        };

        db.ImportExportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(jobId, cancellationToken);

        return new ImportExportJobAcceptedResponse { JobId = jobId };
    }

    public async Task<IReadOnlyList<ImportExportJobResponse>> ListJobsAsync(
        Guid organizationId,
        Guid actorUserId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var jobs = await db.ImportExportJobs.AsNoTracking()
            .Where(j => j.OrganizationId == organizationId)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return jobs.Select(MapJob).ToList();
    }

    public async Task<ImportExportJobResponse?> GetJobAsync(
        Guid organizationId,
        Guid jobId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var job = await db.ImportExportJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.OrganizationId == organizationId && j.Id == jobId, cancellationToken);

        return job is null ? null : MapJob(job);
    }

    public async Task<ImportExportDownloadResponse?> GetDownloadAsync(
        Guid organizationId,
        Guid jobId,
        string artifact,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var blobStorage = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();

        var job = await db.ImportExportJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.OrganizationId == organizationId && j.Id == jobId, cancellationToken);

        if (job is null)
            return null;

        var path = artifact.Trim().ToLowerInvariant() switch
        {
            "source" => job.SourceBlobPath,
            "result" => job.ResultBlobPath,
            "errors" => job.ErrorBlobPath,
            _ => throw new OrganizationValidationException("artifact must be source, result, or errors.")
        };

        if (string.IsNullOrWhiteSpace(path))
            throw new OrganizationNotFoundException("Artifact was not found for this job.");

        var uri = blobStorage.GetReadSasUri(path)
            ?? throw new OrganizationAvatarStorageUnavailableException();

        return new ImportExportDownloadResponse { Url = uri.ToString() };
    }

    async Task EnsureOrgExistsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var exists = await db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);
        if (!exists)
            throw new OrganizationNotFoundException("Organization was not found.");
    }

    static async Task<IReadOnlyList<ImportCsvRow>> ParseFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            throw new OrganizationValidationException("CSV file is required.");

        await using var stream = file.OpenReadStream();
        try
        {
            return ImportCsvParser.Parse(stream);
        }
        catch (InvalidOperationException ex)
        {
            throw new OrganizationValidationException(ex.Message);
        }
    }

    internal async Task<IReadOnlyList<ImportPreviewRow>> ValidateRowsAsync(
        Guid organizationId,
        Guid actorUserId,
        IReadOnlyList<ImportCsvRow> rows,
        CancellationToken cancellationToken)
    {
        var emails = rows.Select(r => r.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Cast<string>().ToList();
        var usernames = rows.Select(r => r.Username).Where(u => !string.IsNullOrWhiteSpace(u)).Cast<string>().ToList();

        var resolved = await authUsers.ResolveUsersAsync(emails, usernames, cancellationToken);
        var byEmail = resolved.Users
            .GroupBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var byUsername = resolved.Users
            .GroupBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var orgRoles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Scope == RoleScope.Org)
            .ToListAsync(cancellationToken);
        var orgRolesByName = orgRoles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var teams = await db.Teams.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var teamsByName = teams.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var teamRoles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Scope == RoleScope.Team)
            .ToListAsync(cancellationToken);
        var teamRolesByName = teamRoles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var actorIsOwner = await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken);

        var preview = new List<ImportPreviewRow>(rows.Count);
        foreach (var row in rows)
        {
            var errors = new List<ImportRowError>();
            Guid? userId = null;

            if (string.IsNullOrWhiteSpace(row.Email) && string.IsNullOrWhiteSpace(row.Username))
            {
                errors.Add(new ImportRowError
                {
                    Code = ImportErrorCodes.ValidationError,
                    Message = "Either email or username is required."
                });
            }

            if (string.IsNullOrWhiteSpace(row.OrgRoles))
            {
                errors.Add(new ImportRowError
                {
                    Code = ImportErrorCodes.ValidationError,
                    Message = "org_roles is required."
                });
            }

            AuthUserProfile? matched = null;
            if (!string.IsNullOrWhiteSpace(row.Email) || !string.IsNullOrWhiteSpace(row.Username))
            {
                var emailMatches = !string.IsNullOrWhiteSpace(row.Email) && byEmail.TryGetValue(row.Email, out var em)
                    ? em
                    : [];
                var usernameMatches = !string.IsNullOrWhiteSpace(row.Username) && byUsername.TryGetValue(row.Username, out var um)
                    ? um
                    : [];

                var candidates = emailMatches
                    .Concat(usernameMatches)
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .ToList();

                if (candidates.Count == 0)
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.UserNotFound,
                        Message = "User does not exist."
                    });
                }
                else if (candidates.Count > 1)
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.AmbiguousIdentity,
                        Message = "Email and username resolve to different users."
                    });
                }
                else if (!string.IsNullOrWhiteSpace(row.Email) && !string.IsNullOrWhiteSpace(row.Username)
                         && emailMatches.Count > 0 && usernameMatches.Count > 0
                         && emailMatches[0].Id != usernameMatches[0].Id)
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.AmbiguousIdentity,
                        Message = "Email and username resolve to different users."
                    });
                }
                else
                {
                    matched = candidates[0];
                    userId = matched.Id;
                }
            }

            var roleNames = SplitRoles(row.OrgRoles);
            foreach (var roleName in roleNames)
            {
                if (!orgRolesByName.TryGetValue(roleName, out var role))
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.InvalidOrgRole,
                        Message = $"Organization role '{roleName}' was not found."
                    });
                }
                else if (role.Name == SystemRoleNames.Owner && !actorIsOwner)
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.OwnerAssignmentForbidden,
                        Message = "Only owners can assign the Owner role."
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(row.Team))
            {
                if (!teamsByName.ContainsKey(row.Team))
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.InvalidTeam,
                        Message = $"Team '{row.Team}' was not found."
                    });
                }

                var teamRoleName = string.IsNullOrWhiteSpace(row.TeamRole) ? SystemRoleNames.Member : row.TeamRole;
                if (!teamRolesByName.ContainsKey(teamRoleName))
                {
                    errors.Add(new ImportRowError
                    {
                        Code = ImportErrorCodes.InvalidTeamRole,
                        Message = $"Team role '{teamRoleName}' was not found."
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(row.TeamRole))
            {
                errors.Add(new ImportRowError
                {
                    Code = ImportErrorCodes.ValidationError,
                    Message = "team_role requires team."
                });
            }

            preview.Add(new ImportPreviewRow
            {
                RowNumber = row.RowNumber,
                Status = errors.Count == 0 ? "valid" : "error",
                Email = row.Email,
                Username = row.Username,
                ResolvedUserId = userId,
                OrgRoles = row.OrgRoles,
                Team = row.Team,
                TeamRole = row.TeamRole,
                JobTitle = row.JobTitle,
                Errors = errors
            });
        }

        return preview;
    }

    internal static IReadOnlyList<string> SplitRoles(string? orgRoles) =>
        string.IsNullOrWhiteSpace(orgRoles)
            ? []
            : orgRoles.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    static ImportExportFormat ParseFormat(string? format) =>
        format?.Trim().ToLowerInvariant() switch
        {
            "json" => ImportExportFormat.Json,
            "csv" => ImportExportFormat.Csv,
            null or "" => ImportExportFormat.Csv,
            _ => throw new OrganizationValidationException("format must be csv or json.")
        };

    static ImportExportJobResponse MapJob(ImportExportJob job) => new()
    {
        Id = job.Id,
        Type = job.Type.ToString().ToLowerInvariant(),
        Status = ToStatusString(job.Status),
        Format = job.Format.ToString().ToLowerInvariant(),
        CreatedByUserId = job.CreatedByUserId,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        TotalRows = job.TotalRows,
        SuccessCount = job.SuccessCount,
        ErrorCount = job.ErrorCount,
        ErrorMessage = job.ErrorMessage,
        HasSource = !string.IsNullOrWhiteSpace(job.SourceBlobPath),
        HasResult = !string.IsNullOrWhiteSpace(job.ResultBlobPath),
        HasErrors = !string.IsNullOrWhiteSpace(job.ErrorBlobPath)
    };

    static string ToStatusString(ImportExportJobStatus status) => status switch
    {
        ImportExportJobStatus.Queued => "queued",
        ImportExportJobStatus.Running => "running",
        ImportExportJobStatus.Completed => "completed",
        ImportExportJobStatus.CompletedWithErrors => "completed_with_errors",
        ImportExportJobStatus.Failed => "failed",
        _ => status.ToString().ToLowerInvariant()
    };
}
