using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

public partial class OrganizationsController
{
    /// <summary>Create organization and add creator as Owner member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId();
        logger.LogInformation("Creating organization {OrganizationName} for user {UserId}", request.Name, userId);

        try
        {
            var organization = await organizationService.CreateAsync(request, userId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { orgId = organization.Id }, organization);
        }
        catch (OrganizationConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Organization creation failed.",
                Detail = ex.Message
            });
        }
    }
}
