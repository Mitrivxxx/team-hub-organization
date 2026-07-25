using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>Transfer organization ownership.</summary>
    [HttpPost("{orgId:guid}/transfer-ownership")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TransferOwnership(Guid orgId, TransferOwnershipRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            await organizationService.TransferOwnershipAsync(orgId, userId, request.NewOwnerUserId, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Leave organization.</summary>
    [HttpPost("{orgId:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            await organizationService.LeaveAsync(orgId, userId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
