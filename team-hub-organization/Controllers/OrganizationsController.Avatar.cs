using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>Upload organization avatar.</summary>
    [HttpPut("{orgId:guid}/avatar")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(Guid orgId, IFormFile file, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var organization = await organizationAvatarService.UploadAsync(orgId, file, userId, cancellationToken);
            return organization is null ? NotFound() : Ok(organization);
        }
        catch (OrganizationAvatarStorageUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Blob storage is not configured.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (OrganizationAvatarValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid avatar file.",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (OrganizationAccessException ex)
        {
            return ForbidWithMessage(ex.Message);
        }
    }

    /// <summary>Delete organization avatar.</summary>
    [HttpDelete("{orgId:guid}/avatar")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteAvatar(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var organization = await organizationAvatarService.DeleteAsync(orgId, userId, cancellationToken);
            return organization is null ? NotFound() : Ok(organization);
        }
        catch (OrganizationAvatarStorageUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Blob storage is not configured.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (OrganizationAccessException ex)
        {
            return ForbidWithMessage(ex.Message);
        }
    }
}
