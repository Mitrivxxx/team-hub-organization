using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>Soft-delete organization.</summary>
    [HttpDelete("{orgId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var deleted = await organizationService.SoftDeleteAsync(orgId, userId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
