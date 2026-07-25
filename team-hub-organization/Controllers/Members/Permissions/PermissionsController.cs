using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Members.Permissions;

namespace team_hub_organization.Controllers.Members.Permissions;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public sealed class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    /// <summary>List permission catalog.</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var permissions = await permissionService.ListAsync(cancellationToken);
        return Ok(permissions);
    }
}
