using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Controllers;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController
{
    /// <summary>Create organization and add creator as Owner member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();
        logger.LogInformation("Creating organization {OrganizationName} for user {UserId}", request.Name, userId);

        try
        {
            var organization = await organizationService.CreateAsync(request, userId, cancellationToken);
            return CreatedAtVersionedAction(nameof(GetById), new { orgId = organization.Id }, organization);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
