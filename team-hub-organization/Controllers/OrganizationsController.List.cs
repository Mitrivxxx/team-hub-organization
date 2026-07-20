using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>List current user organizations.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();
        var organizations = await organizationService.ListForUserAsync(userId, cancellationToken);
        return Ok(organizations);
    }
}
