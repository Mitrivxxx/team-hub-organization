using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>Soft-delete organization.</summary>
    [HttpDelete("{orgId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var deleted = await organizationService.SoftDeleteAsync(orgId, userId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (OrganizationAccessException ex)
        {
            return ForbidWithMessage(ex.Message);
        }
    }

    ObjectResult ForbidWithMessage(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden.",
            Detail = detail
        });
}
