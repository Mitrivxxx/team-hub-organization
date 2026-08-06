using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Statistics;

namespace team_hub_organization.Controllers.Statistics;

public sealed class StatisticsController(
    IOrganizationStatsService statsService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>Get organization member and team counts.</summary>
    [HttpGet("{orgId:guid}/stats")]
    [ProducesResponseType(typeof(OrganizationStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, CancellationToken cancellationToken)
    {
        var result = await statsService.GetAsync(
            orgId,
            currentUserService.GetRequiredUserId(),
            cancellationToken);
        return Ok(result);

    }
}
