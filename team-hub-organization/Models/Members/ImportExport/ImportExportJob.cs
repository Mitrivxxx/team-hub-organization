namespace team_hub_organization.Models;

public enum ImportExportJobType
{
    Import = 0,
    Export = 1
}

public enum ImportExportJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    CompletedWithErrors = 3,
    Failed = 4
}

public enum ImportExportFormat
{
    Csv = 0,
    Json = 1
}

public sealed class ImportExportJob
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public ImportExportJobType Type { get; set; }
    public ImportExportJobStatus Status { get; set; }
    public ImportExportFormat Format { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? SourceBlobPath { get; set; }
    public string? ResultBlobPath { get; set; }
    public string? ErrorBlobPath { get; set; }
    public string? OptionsJson { get; set; }
    public string? ErrorMessage { get; set; }
}
