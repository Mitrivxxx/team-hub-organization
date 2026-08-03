using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Statistics;

namespace team_hub_organization.Controllers.Statistics;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public sealed class StatisticsController(
    IOrganizationStatsService statsService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>Get organization member and team counts.</summary>
    [HttpGet("{orgId:guid}/stats")]
    [ProducesResponseType(typeof(OrganizationStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid orgId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await statsService.GetAsync(
                orgId,
                currentUserService.GetRequiredUserId(),
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
