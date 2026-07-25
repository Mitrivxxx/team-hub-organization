using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.Permissions;

namespace team_hub_organization.Controllers.Members.Permissions;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public sealed class PermissionsController(
    IPermissionService permissionService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>List organization permissions.</summary>
    [HttpGet("{orgId:guid}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionListItemResponse>), StatusCodes.Status200OK)]
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
    public async Task<IActionResult> Create(Guid orgId, CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var permission = await permissionService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { orgId, permissionId = permission.Id }, permission);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Update organization permission.</summary>
    [HttpPatch("{orgId:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(typeof(PermissionDetailResponse), StatusCodes.Status200OK)]
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
