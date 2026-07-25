using FluentValidation;
using team_hub_organization.Dtos;

namespace team_hub_organization.Validators;

public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

public sealed class UpdateTeamRequestValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Scope).NotEmpty().Must(s =>
            string.Equals(s, "ORG", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "TEAM", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.OrgRoleId).NotEmpty();
    }
}

public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class AddTeamMemberRequestValidator : AbstractValidator<AddTeamMemberRequest>
{
    public AddTeamMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.JobTitle).MaximumLength(100).When(x => x.JobTitle is not null);
    }
}

public sealed class TransferOwnershipRequestValidator : AbstractValidator<TransferOwnershipRequest>
{
    public TransferOwnershipRequestValidator()
    {
        RuleFor(x => x.NewOwnerUserId).NotEmpty();
    }
}
