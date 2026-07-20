using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>Update organization name and avatar URL.</summary>
    [HttpPatch("{orgId:guid}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid orgId, UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var organization = await organizationService.UpdateAsync(orgId, request, userId, cancellationToken);
            return organization is null ? NotFound() : Ok(organization);
        }
        catch (OrganizationAccessException ex)
        {
            return ForbidWithMessage(ex.Message);
        }
    }
}
