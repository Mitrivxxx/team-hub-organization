using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Teams;

namespace team_hub_organization.Controllers;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public sealed class TeamsController(
    ITeamService teamService,
    ITeamAvatarService teamAvatarService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>List teams.</summary>
    [HttpGet("{orgId:guid}/teams")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await teamService.ListAsync(orgId, currentUserService.GetRequiredUserId(), cancellationToken));
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Create team.</summary>
    [HttpPost("{orgId:guid}/teams")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid orgId, CreateTeamRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await teamService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { orgId, teamId = team.Id }, team);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Get team.</summary>
    [HttpGet("{orgId:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        try
        {
            var team = await teamService.GetAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken);
            return team is null ? NotFound() : Ok(team);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Update team.</summary>
    [HttpPatch("{orgId:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid orgId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await teamService.UpdateAsync(orgId, teamId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return team is null ? NotFound() : Ok(team);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Soft-delete team.</summary>
    [HttpDelete("{orgId:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await teamService.SoftDeleteAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Upload team avatar.</summary>
    [HttpPut("{orgId:guid}/teams/{teamId:guid}/avatar")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(Guid orgId, Guid teamId, IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var team = await teamAvatarService.UploadAsync(orgId, teamId, file, currentUserService.GetRequiredUserId(), cancellationToken);
            return team is null ? NotFound() : Ok(team);
        }
        catch (OrganizationAvatarStorageUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Blob storage is not configured.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (OrganizationAvatarValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid avatar file.",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Delete team avatar.</summary>
    [HttpDelete("{orgId:guid}/teams/{teamId:guid}/avatar")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAvatar(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        try
        {
            var team = await teamAvatarService.DeleteAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken);
            return team is null ? NotFound() : Ok(team);
        }
        catch (OrganizationAvatarStorageUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Blob storage is not configured.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>List team members.</summary>
    [HttpGet("{orgId:guid}/teams/{teamId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembers(Guid orgId, Guid teamId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await teamService.ListMembersAsync(orgId, teamId, currentUserService.GetRequiredUserId(), cancellationToken));
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Add team member.</summary>
    [HttpPost("{orgId:guid}/teams/{teamId:guid}/members")]
    [ProducesResponseType(typeof(TeamMemberResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMember(Guid orgId, Guid teamId, AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var member = await teamService.AddMemberAsync(orgId, teamId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return CreatedAtAction(nameof(ListMembers), new { orgId, teamId }, member);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Update team member.</summary>
    [HttpPatch("{orgId:guid}/teams/{teamId:guid}/members/{userId:guid}")]
    [ProducesResponseType(typeof(TeamMemberResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMember(Guid orgId, Guid teamId, Guid userId, UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var member = await teamService.UpdateMemberAsync(orgId, teamId, userId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return member is null ? NotFound() : Ok(member);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Remove team member.</summary>
    [HttpDelete("{orgId:guid}/teams/{teamId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid orgId, Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await teamService.RemoveMemberAsync(orgId, teamId, userId, currentUserService.GetRequiredUserId(), cancellationToken);
            return removed ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
