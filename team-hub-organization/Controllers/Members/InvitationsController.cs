using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services;
using team_hub_organization.Services.Members.Invitations;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers.Members;

public sealed class InvitationsController(
    IInvitationService invitationService,
    ICurrentUserService currentUserService) : OrganizationApiController
{
    /// <summary>List organization invitations.</summary>
    [HttpGet("{orgId:guid}/invitations")]
    [ProducesResponseType(typeof(IReadOnlyList<InvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid orgId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        InvitationStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<InvitationStatus>(status, ignoreCase: true, out var value))
                throw new OrganizationValidationException("Invalid invitation status.");
            parsed = value;
        }

        return Ok(await invitationService.ListAsync(orgId, currentUserService.GetRequiredUserId(), parsed, cancellationToken));
    }

    /// <summary>Create invitation.</summary>
    [HttpPost("{orgId:guid}/invitations")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid orgId, CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        var invitation = await invitationService.CreateAsync(orgId, request, currentUserService.GetRequiredUserId(), cancellationToken);
        return CreatedAtVersionedAction(nameof(Get), new { orgId, invitationId = invitation.Id }, invitation);

    }

    /// <summary>Get invitation.</summary>
    [HttpGet("{orgId:guid}/invitations/{invitationId:guid}")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orgId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitationService.GetAsync(orgId, invitationId, currentUserService.GetRequiredUserId(), cancellationToken);
        return invitation is null ? NotFound() : Ok(invitation);

    }

    /// <summary>Cancel invitation.</summary>
    [HttpDelete("{orgId:guid}/invitations/{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid orgId, Guid invitationId, CancellationToken cancellationToken)
    {
        await invitationService.CancelAsync(orgId, invitationId, currentUserService.GetRequiredUserId(), cancellationToken);
        return NoContent();

    }

    /// <summary>Resend invitation.</summary>
    [HttpPost("{orgId:guid}/invitations/{invitationId:guid}/resend")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resend(Guid orgId, Guid invitationId, CancellationToken cancellationToken)
    {
        return Ok(await invitationService.ResendAsync(orgId, invitationId, currentUserService.GetRequiredUserId(), cancellationToken));

    }

    /// <summary>Preview invitation by token.</summary>
    [HttpGet("invitations/by-token/{token}")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByToken(string token, CancellationToken cancellationToken)
    {
        var invitation = await invitationService.GetByTokenAsync(token, cancellationToken);
        return invitation is null ? NotFound() : Ok(invitation);

    }

    /// <summary>Accept invitation by token.</summary>
    [HttpPost("invitations/by-token/{token}/accept")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        return Ok(await invitationService.AcceptAsync(token, currentUserService.GetRequiredUserId(), cancellationToken));

    }

    /// <summary>Reject invitation by token.</summary>
    [HttpPost("invitations/by-token/{token}/reject")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(string token, CancellationToken cancellationToken)
    {
        return Ok(await invitationService.RejectAsync(token, currentUserService.GetRequiredUserId(), cancellationToken));

    }

    /// <summary>List pending invitations for current user email claim.</summary>
    [HttpGet("me/invitations")]
    [ProducesResponseType(typeof(IReadOnlyList<InvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue("email");
        return Ok(await invitationService.ListMineAsync(email, cancellationToken));
    }
}
