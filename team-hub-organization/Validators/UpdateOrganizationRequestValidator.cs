using FluentValidation;
using team_hub_organization.Dtos;

namespace team_hub_organization.Validators;

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.Name is not null);

        When(x => x.Nip is not null, () =>
        {
            RuleFor(x => x.Nip!)
                .NotEmpty()
                .MaximumLength(20)
                .Matches(@"^\d{10}$")
                .WithMessage("NIP must be exactly 10 digits.");
        });

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Country).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Address!.PostalCode).NotEmpty().MaximumLength(20);
        });
    }
}
