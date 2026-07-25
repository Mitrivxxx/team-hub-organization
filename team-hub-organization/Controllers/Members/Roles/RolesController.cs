using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.Roles;

namespace team_hub_organization.Controllers.Members.Roles;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public sealed class RolesController(
    IRoleService roleService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>List organization roles.</summary>
    [HttpGet("{orgId:guid}/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid orgId, [FromQuery] string? scope, CancellationToken cancellationToken)
    {
        try
        {
            RoleScope? parsed = null;
            if (!string.IsNullOrWhiteSpace(scope))
            {
                if (string.Equals(scope, "ORG", StringComparison.OrdinalIgnoreCase))
                    parsed = RoleScope.Org;
                else if (string.Equals(scope, "TEAM", StringComparison.OrdinalIgnoreCase))
                    parsed = RoleScope.Team;
                else
                    return BadRequest(new ProblemDetails { Title = "Scope must be ORG or TEAM.", Status = 400 });
            }

            var roles = await roleService.ListAsync(orgId, currentUserService.GetRequiredUserId(), parsed, cancellationToken);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Get organization role.</summary>
    [HttpGet("{orgId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        try
        {
            var role = await roleService.GetAsync(orgId, roleId, currentUserService.GetRequiredUserId(), cancellationToken);
            return role is null ? NotFound() : Ok(role);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Create organization role.</summary>
    [HttpPost("{orgId:guid}/roles")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid orgId, CreateRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await roleService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { orgId, roleId = role.Id }, role);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Update organization role.</summary>
    [HttpPatch("{orgId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid orgId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await roleService.UpdateAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return role is null ? NotFound() : Ok(role);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Delete organization role.</summary>
    [HttpDelete("{orgId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        try
        {
            await roleService.DeleteAsync(orgId, roleId, currentUserService.GetRequiredUserId(), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Replace role permissions.</summary>
    [HttpPut("{orgId:guid}/roles/{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplacePermissions(Guid orgId, Guid roleId, RolePermissionsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await roleService.ReplacePermissionsAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return role is null ? NotFound() : Ok(role);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Add role permissions.</summary>
    [HttpPost("{orgId:guid}/roles/{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPermissions(Guid orgId, Guid roleId, RolePermissionsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await roleService.AddPermissionsAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return role is null ? NotFound() : Ok(role);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Remove role permission.</summary>
    [HttpDelete("{orgId:guid}/roles/{roleId:guid}/permissions/{permissionCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemovePermission(Guid orgId, Guid roleId, string permissionCode, CancellationToken cancellationToken)
    {
        try
        {
            await roleService.RemovePermissionAsync(orgId, roleId, permissionCode, currentUserService.GetRequiredUserId(), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
