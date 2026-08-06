using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Controllers;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>Update organization name.</summary>
    [HttpPatch("{orgId:guid}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid orgId, UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        var organization = await organizationService.UpdateAsync(orgId, request, userId, cancellationToken);
        return organization is null ? NotFound() : Ok(organization);

    }
}
