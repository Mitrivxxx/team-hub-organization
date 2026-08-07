using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>Update organization status (active, suspended, archived).</summary>
    [HttpPatch("{orgId:guid}/status")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid orgId,
        UpdateOrganizationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();
        var organization = await organizationService.UpdateStatusAsync(orgId, request, userId, cancellationToken);
        return organization is null ? NotFound() : Ok(organization);
    }
}
