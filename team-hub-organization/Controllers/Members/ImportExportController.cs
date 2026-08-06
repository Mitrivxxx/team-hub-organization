using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.ImportExport;

namespace team_hub_organization.Controllers.Members;

public sealed class ImportExportController(
    IImportExportService importExportService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>Preview member CSV import without writing.</summary>
    [HttpPost("{orgId:guid}/imports/preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewImport(Guid orgId, IFormFile file, CancellationToken cancellationToken)
    {
        var preview = await importExportService.PreviewImportAsync(
            orgId, file, currentUserService.GetRequiredUserId(), cancellationToken);
        return Ok(preview);

    }

    /// <summary>Start async member CSV import.</summary>
    [HttpPost("{orgId:guid}/imports")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportExportJobAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartImport(Guid orgId, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await importExportService.StartImportAsync(
            orgId, file, currentUserService.GetRequiredUserId(), cancellationToken);
        return AcceptedAtVersionedAction(nameof(GetJob), new { orgId, jobId = result.JobId }, result);

    }

    /// <summary>Start async organization data export.</summary>
    [HttpPost("{orgId:guid}/exports")]
    [ProducesResponseType(typeof(ImportExportJobAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartExport(
        Guid orgId,
        [FromBody] CreateExportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await importExportService.StartExportAsync(
            orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return AcceptedAtVersionedAction(nameof(GetJob), new { orgId, jobId = result.JobId }, result);

    }

    /// <summary>List import/export jobs.</summary>
    [HttpGet("{orgId:guid}/import-export/jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<ImportExportJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListJobs(
        Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await importExportService.ListJobsAsync(
            orgId, currentUserService.GetRequiredUserId(), page, pageSize, cancellationToken);
        return Ok(jobs);

    }

    /// <summary>Get import/export job status.</summary>
    [HttpGet("{orgId:guid}/import-export/jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ImportExportJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid orgId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await importExportService.GetJobAsync(
            orgId, jobId, currentUserService.GetRequiredUserId(), cancellationToken);
        return job is null ? NotFound() : Ok(job);

    }

    /// <summary>Get SAS URL for job artifact download.</summary>
    [HttpGet("{orgId:guid}/import-export/jobs/{jobId:guid}/download")]
    [ProducesResponseType(typeof(ImportExportDownloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Download(
        Guid orgId,
        Guid jobId,
        [FromQuery] string artifact = "result",
        CancellationToken cancellationToken = default)
    {
        var result = await importExportService.GetDownloadAsync(
            orgId, jobId, artifact, currentUserService.GetRequiredUserId(), cancellationToken);
        return result is null ? NotFound() : Ok(result);

    }
}
