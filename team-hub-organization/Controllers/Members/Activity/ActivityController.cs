using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.Activity;

namespace team_hub_organization.Controllers.Members.Activity;

public sealed class ActivityController(
    IActivityService activityService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>List organization activity feed.</summary>
    [HttpGet("{orgId:guid}/activity")]
    [ProducesResponseType(typeof(ActivityPageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        Guid orgId,
        [FromQuery] string? type,
        [FromQuery] string? q,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await activityService.ListAsync(
                orgId,
                currentUserService.GetRequiredUserId(),
                type,
                q,
                from,
                to,
                page,
                pageSize,
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
