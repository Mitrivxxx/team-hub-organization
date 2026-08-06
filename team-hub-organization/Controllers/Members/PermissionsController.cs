using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.Permissions;

namespace team_hub_organization.Controllers.Members;

public sealed class PermissionsController(
    IPermissionService permissionService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>List organization permissions.</summary>
    [HttpGet("{orgId:guid}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        try
        {
            var permissions = await permissionService.ListAsync(orgId, currentUserService.GetRequiredUserId(), cancellationToken);
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Get organization permission.</summary>
    [HttpGet("{orgId:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(typeof(PermissionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, Guid permissionId, CancellationToken cancellationToken)
    {
        try
        {
            var permission = await permissionService.GetAsync(orgId, permissionId, currentUserService.GetRequiredUserId(), cancellationToken);
            return permission is null ? NotFound() : Ok(permission);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Create organization permission.</summary>
    [HttpPost("{orgId:guid}/permissions")]
    [ProducesResponseType(typeof(PermissionDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid orgId, CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var permission = await permissionService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return CreatedAtVersionedAction(nameof(Get), new { orgId, permissionId = permission.Id }, permission);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Update organization permission.</summary>
    [HttpPatch("{orgId:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(typeof(PermissionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid orgId, Guid permissionId, UpdatePermissionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var permission = await permissionService.UpdateAsync(orgId, permissionId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return permission is null ? NotFound() : Ok(permission);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Delete organization permission.</summary>
    [HttpDelete("{orgId:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid orgId, Guid permissionId, CancellationToken cancellationToken)
    {
        try
        {
            await permissionService.DeleteAsync(orgId, permissionId, currentUserService.GetRequiredUserId(), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
