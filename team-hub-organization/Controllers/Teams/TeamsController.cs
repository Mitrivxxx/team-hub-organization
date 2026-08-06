using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Teams;

namespace team_hub_organization.Controllers.Teams;

public sealed class TeamsController(
    ITeamService teamService,
    ITeamAvatarService teamAvatarService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>List teams.</summary>
    [HttpGet("{orgId:guid}/teams")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        return Ok(await teamService.ListAsync(orgId, currentUserService.GetRequiredUserId(), cancellationToken));

    }

    /// <summary>Create team.</summary>
    [HttpPost("{orgId:guid}/teams")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid orgId, CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await teamService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return CreatedAtVersionedAction(nameof(Get), new { orgId, teamId = team.Id }, team);

    }

    /// <summary>Get team.</summary>
    [HttpGet("{orgId:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await teamService.GetAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken);
        return team is null ? NotFound() : Ok(team);

    }

    /// <summary>Update team.</summary>
    [HttpPatch("{orgId:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid orgId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await teamService.UpdateAsync(orgId, teamId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return team is null ? NotFound() : Ok(team);

    }

    /// <summary>Soft-delete team.</summary>
    [HttpDelete("{orgId:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        var deleted = await teamService.SoftDeleteAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken);
        return deleted ? NoContent() : NotFound();

    }

    /// <summary>Upload team avatar.</summary>
    [HttpPut("{orgId:guid}/teams/{teamId:guid}/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(Guid orgId, Guid teamId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var team = await teamAvatarService.UploadAsync(orgId, teamId, file, currentUserService.GetRequiredUserId(), cancellationToken);
        return team is null ? NotFound() : Ok(team);

    }

    /// <summary>Delete team avatar.</summary>
    [HttpDelete("{orgId:guid}/teams/{teamId:guid}/avatar")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteAvatar(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await teamAvatarService.DeleteAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken);
        return team is null ? NotFound() : Ok(team);

    }

    /// <summary>List team members.</summary>
    [HttpGet("{orgId:guid}/teams/{teamId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        return Ok(await teamService.ListMembersAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken));

    }

    /// <summary>Add team member.</summary>
    [HttpPost("{orgId:guid}/teams/{teamId:guid}/members")]
    [ProducesResponseType(typeof(TeamMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(Guid orgId, Guid teamId, AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await teamService.AddMemberAsync(orgId, teamId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return CreatedAtVersionedAction(nameof(ListMembers), new { orgId, teamId }, member);

    }

    /// <summary>Update team member.</summary>
    [HttpPatch("{orgId:guid}/teams/{teamId:guid}/members/{userId:guid}")]
    [ProducesResponseType(typeof(TeamMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember(Guid orgId, Guid teamId, Guid userId, UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await teamService.UpdateMemberAsync(orgId, teamId, userId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return member is null ? NotFound() : Ok(member);

    }

    /// <summary>Remove team member.</summary>
    [HttpDelete("{orgId:guid}/teams/{teamId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid orgId, Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var removed = await teamService.RemoveMemberAsync(orgId, teamId, userId, currentUserService.GetRequiredUserId(), cancellationToken);
        return removed ? NoContent() : NotFound();

    }
}
