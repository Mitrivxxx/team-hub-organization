using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.AllMembers;

namespace team_hub_organization.Controllers.Members;

public sealed class MembersController(
    IMemberService memberService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>List organization members.</summary>
    [HttpGet("{orgId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<MemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid orgId,
        [FromQuery] Guid? roleId,
        [FromQuery] Guid? teamId,
        CancellationToken cancellationToken)
    {
        try
        {
            var members = await memberService.ListAsync(
                orgId,
                currentUserService.GetRequiredUserId(),
                roleId,
                teamId,
                cancellationToken);
            return Ok(members);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Get organization member.</summary>
    [HttpGet("{orgId:guid}/members/{userId:guid}")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var member = await memberService.GetAsync(orgId, userId, currentUserService.GetRequiredUserId(), cancellationToken);
            return member is null ? NotFound() : Ok(member);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Add organization member.</summary>
    [HttpPost("{orgId:guid}/members")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid orgId, AddMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var member = await memberService.AddAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return CreatedAtVersionedAction(nameof(Get), new { orgId, userId = member.UserId }, member);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Update organization member role.</summary>
    [HttpPatch("{orgId:guid}/members/{userId:guid}")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid orgId, Guid userId, UpdateMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var member = await memberService.UpdateAsync(orgId, userId, request, currentUserService.GetRequiredUserId(), cancellationToken);
            return member is null ? NotFound() : Ok(member);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>Remove organization member.</summary>
    [HttpDelete("{orgId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid orgId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await memberService.RemoveAsync(orgId, userId, currentUserService.GetRequiredUserId(), cancellationToken);
            return removed ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }

    /// <summary>List member teams in organization.</summary>
    [HttpGet("{orgId:guid}/members/{userId:guid}/teams")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamMembershipResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListTeams(Guid orgId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var teams = await memberService.ListTeamsAsync(orgId, userId, currentUserService.GetRequiredUserId(), cancellationToken);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            return ControllerExceptionMapper.Map(ex);
        }
    }
}
