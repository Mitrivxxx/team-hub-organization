using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.Roles;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers.Members;

public sealed class RolesController(
    IRoleService roleService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>List organization roles.</summary>
    [HttpGet("{orgId:guid}/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid orgId, [FromQuery] string? scope, CancellationToken cancellationToken)
    {
        RoleScope? parsed = null;
        if (!string.IsNullOrWhiteSpace(scope))
        {
            if (string.Equals(scope, "ORG", StringComparison.OrdinalIgnoreCase))
                parsed = RoleScope.Org;
            else if (string.Equals(scope, "TEAM", StringComparison.OrdinalIgnoreCase))
                parsed = RoleScope.Team;
            else
                throw new OrganizationValidationException("Scope must be ORG or TEAM.");
        }

        var roles = await roleService.ListAsync(orgId, currentUserService.GetRequiredUserId(), parsed, cancellationToken);
        return Ok(roles);
    }

    /// <summary>Get organization role.</summary>
    [HttpGet("{orgId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(typeof(RoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await roleService.GetAsync(orgId, roleId, currentUserService.GetRequiredUserId(), cancellationToken);
        return role is null ? NotFound() : Ok(role);

    }

    /// <summary>Create organization role.</summary>
    [HttpPost("{orgId:guid}/roles")]
    [ProducesResponseType(typeof(RoleDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid orgId, CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roleService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return CreatedAtVersionedAction(nameof(Get), new { orgId, roleId = role.Id }, role);

    }

    /// <summary>Update organization role.</summary>
    [HttpPatch("{orgId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(typeof(RoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid orgId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roleService.UpdateAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return role is null ? NotFound() : Ok(role);

    }

    /// <summary>Delete organization role.</summary>
    [HttpDelete("{orgId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        await roleService.DeleteAsync(orgId, roleId, currentUserService.GetRequiredUserId(), cancellationToken);
        return NoContent();

    }

    /// <summary>List role permissions.</summary>
    [HttpGet("{orgId:guid}/roles/{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListPermissions(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        var permissions = await roleService.ListPermissionsAsync(orgId, roleId, currentUserService.GetRequiredUserId(), cancellationToken);
        return Ok(permissions);

    }

    /// <summary>Replace role permissions.</summary>
    [HttpPut("{orgId:guid}/roles/{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplacePermissions(Guid orgId, Guid roleId, AssignRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var permissions = await roleService.ReplacePermissionsAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return permissions is null ? NotFound() : Ok(permissions);

    }

    /// <summary>Add role permissions.</summary>
    [HttpPost("{orgId:guid}/roles/{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPermissions(Guid orgId, Guid roleId, AssignRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var permissions = await roleService.AddPermissionsAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return permissions is null ? NotFound() : Ok(permissions);

    }

    /// <summary>Remove role permission.</summary>
    [HttpDelete("{orgId:guid}/roles/{roleId:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePermission(Guid orgId, Guid roleId, Guid permissionId, CancellationToken cancellationToken)
    {
        await roleService.RemovePermissionAsync(orgId, roleId, permissionId, currentUserService.GetRequiredUserId(), cancellationToken);
        return NoContent();

    }

    /// <summary>List role members.</summary>
    [HttpGet("{orgId:guid}/roles/{roleId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<MemberSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        var members = await roleService.ListMembersAsync(orgId, roleId, currentUserService.GetRequiredUserId(), cancellationToken);
        return Ok(members);

    }

    /// <summary>Assign role to member.</summary>
    [HttpPost("{orgId:guid}/roles/{roleId:guid}/members")]
    [ProducesResponseType(typeof(MemberSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignMember(Guid orgId, Guid roleId, AssignRoleMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await roleService.AssignMemberAsync(orgId, roleId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return CreatedAtVersionedAction(nameof(ListMembers), new { orgId, roleId }, member);

    }

    /// <summary>Revoke role from member.</summary>
    [HttpDelete("{orgId:guid}/roles/{roleId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeMember(Guid orgId, Guid roleId, Guid userId, CancellationToken cancellationToken)
    {
        await roleService.RevokeMemberAsync(orgId, roleId, userId, currentUserService.GetRequiredUserId(), cancellationToken);
        return NoContent();

    }
}
