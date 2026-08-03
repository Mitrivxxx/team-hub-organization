namespace team_hub_organization.Dtos;

public sealed class ImportPreviewResponse
{
    public int TotalRows { get; init; }
    public int ValidCount { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyList<ImportPreviewRow> Rows { get; init; } = [];
}

public sealed class ImportPreviewRow
{
    public int RowNumber { get; init; }
    public string Status { get; init; } = "valid";
    public string? Email { get; init; }
    public string? Username { get; init; }
    public Guid? ResolvedUserId { get; init; }
    public string? OrgRoles { get; init; }
    public string? Team { get; init; }
    public string? TeamRole { get; init; }
    public string? JobTitle { get; init; }
    public IReadOnlyList<ImportRowError> Errors { get; init; } = [];
}

public sealed class ImportRowError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class ImportExportJobAcceptedResponse
{
    public Guid JobId { get; init; }
}

public sealed class CreateExportRequest
{
    public string Format { get; init; } = "csv";
    public IReadOnlyList<string> Datasets { get; init; } = ["members", "teams", "roles", "permissions", "organization"];
}

public sealed class ImportExportJobResponse
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "";
    public string Status { get; init; } = "";
    public string Format { get; init; } = "";
    public Guid CreatedByUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int TotalRows { get; init; }
    public int SuccessCount { get; init; }
    public int ErrorCount { get; init; }
    public string? ErrorMessage { get; init; }
    public bool HasSource { get; init; }
    public bool HasResult { get; init; }
    public bool HasErrors { get; init; }
}

public sealed class ImportExportDownloadResponse
{
    public string Url { get; init; } = "";
}

public static class ImportErrorCodes
{
    public const string UserNotFound = "user_not_found";
    public const string AmbiguousIdentity = "ambiguous_identity";
    public const string InvalidOrgRole = "invalid_org_role";
    public const string InvalidTeam = "invalid_team";
    public const string InvalidTeamRole = "invalid_team_role";
    public const string OwnerAssignmentForbidden = "owner_assignment_forbidden";
    public const string ValidationError = "validation_error";
}
