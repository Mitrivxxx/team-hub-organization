using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Me;

namespace team_hub_organization.Controllers.Me;

public sealed class MeController(
    IMeService meService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>Get current membership in organization.</summary>
    [HttpGet("{orgId:guid}/me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, CancellationToken cancellationToken)
    {
        var me = await meService.GetAsync(orgId, currentUserService.GetRequiredUserId(), cancellationToken);
        return me is null ? NotFound() : Ok(me);

    }
}
