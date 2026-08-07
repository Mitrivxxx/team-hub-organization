using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>Restore soft-deleted organization within retention window.</summary>
    [HttpPost("{orgId:guid}/restore")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Restore(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();
        var organization = await organizationService.RestoreAsync(orgId, userId, cancellationToken);
        return Ok(organization);
    }
}
