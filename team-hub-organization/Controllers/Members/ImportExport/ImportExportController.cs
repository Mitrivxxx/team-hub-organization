using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.ImportExport;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers.Members.ImportExport;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public sealed class ImportExportController(
    IImportExportService importExportService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>Preview member CSV import without writing.</summary>
    [HttpPost("{orgId:guid}/imports/preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportPreviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewImport(Guid orgId, IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var preview = await importExportService.PreviewImportAsync(
                orgId, file, currentUserService.GetRequiredUserId(), cancellationToken);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    /// <summary>Start async member CSV import.</summary>
    [HttpPost("{orgId:guid}/imports")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportExportJobAcceptedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartImport(Guid orgId, IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var result = await importExportService.StartImportAsync(
                orgId, file, currentUserService.GetRequiredUserId(), cancellationToken);
            return AcceptedAtAction(nameof(GetJob), new { orgId, jobId = result.JobId }, result);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    /// <summary>Start async organization data export.</summary>
    [HttpPost("{orgId:guid}/exports")]
    [ProducesResponseType(typeof(ImportExportJobAcceptedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartExport(
        Guid orgId,
        [FromBody] CreateExportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await importExportService.StartExportAsync(
                orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return AcceptedAtAction(nameof(GetJob), new { orgId, jobId = result.JobId }, result);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    /// <summary>List import/export jobs.</summary>
    [HttpGet("{orgId:guid}/import-export/jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<ImportExportJobResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobs(
        Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await importExportService.ListJobsAsync(
                orgId, currentUserService.GetRequiredUserId(), page, pageSize, cancellationToken);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    /// <summary>Get import/export job status.</summary>
    [HttpGet("{orgId:guid}/import-export/jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ImportExportJobResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJob(Guid orgId, Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var job = await importExportService.GetJobAsync(
                orgId, jobId, currentUserService.GetRequiredUserId(), cancellationToken);
            return job is null ? NotFound() : Ok(job);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    /// <summary>Get SAS URL for job artifact download.</summary>
    [HttpGet("{orgId:guid}/import-export/jobs/{jobId:guid}/download")]
    [ProducesResponseType(typeof(ImportExportDownloadResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(
        Guid orgId,
        Guid jobId,
        [FromQuery] string artifact = "result",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await importExportService.GetDownloadAsync(
                orgId, jobId, artifact, currentUserService.GetRequiredUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    static IActionResult Map(Exception ex) => ex switch
    {
        OrganizationAvatarStorageUnavailableException => new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Blob storage is not configured."
        })
        { StatusCode = StatusCodes.Status503ServiceUnavailable },
        _ => ControllerExceptionMapper.Map(ex)
    };
}
