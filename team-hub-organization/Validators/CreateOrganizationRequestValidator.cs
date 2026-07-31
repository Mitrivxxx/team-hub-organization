using FluentValidation;
using team_hub_organization.Dtos;

namespace team_hub_organization.Validators;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        When(x => !string.IsNullOrWhiteSpace(x.Slug), () =>
        {
            RuleFor(x => x.Slug!)
                .MaximumLength(100)
                .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        });

        RuleFor(x => x.Nip)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\d{10}$")
            .WithMessage("NIP must be exactly 10 digits.");

        RuleFor(x => x.Address)
            .NotNull()
            .ChildRules(address =>
            {
                address.RuleFor(a => a.Country).NotEmpty().MaximumLength(100);
                address.RuleFor(a => a.City).NotEmpty().MaximumLength(100);
                address.RuleFor(a => a.PostalCode).NotEmpty().MaximumLength(20);
            });
    }
}
