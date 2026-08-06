using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Controllers;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>List current user organizations.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();
        var organizations = await organizationService.ListForUserAsync(userId, cancellationToken);
        return Ok(organizations);
    }
}
