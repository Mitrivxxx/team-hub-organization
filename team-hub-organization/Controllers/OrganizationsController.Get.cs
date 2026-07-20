using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>Get organization by slug.</summary>
    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var organization = await organizationService.GetBySlugAsync(slug, userId, cancellationToken);
            return organization is null ? NotFound() : Ok(organization);
        }
        catch (OrganizationAccessException ex)
        {
            return ForbidWithMessage(ex.Message);
        }
    }

    /// <summary>Get organization details.</summary>
    [HttpGet("{orgId:guid}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();

        try
        {
            var organization = await organizationService.GetByIdAsync(orgId, userId, cancellationToken);
            return organization is null ? NotFound() : Ok(organization);
        }
        catch (OrganizationAccessException ex)
        {
            return ForbidWithMessage(ex.Message);
        }
    }
}
