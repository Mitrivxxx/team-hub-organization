using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>Soft-delete organization (requires recent completed export).</summary>
    [HttpDelete("{orgId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        var deleted = await organizationService.SoftDeleteAsync(orgId, userId, cancellationToken);
        return deleted ? NoContent() : NotFound();

    }
}
