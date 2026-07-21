using FluentValidation;
using team_hub_organization.Dtos;

namespace team_hub_organization.Validators;

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
