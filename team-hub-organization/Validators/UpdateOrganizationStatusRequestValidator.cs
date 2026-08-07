using FluentValidation;
using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Validators;

public sealed class UpdateOrganizationStatusRequestValidator : AbstractValidator<UpdateOrganizationStatusRequest>
{
    static readonly HashSet<string> Allowed =
    [
        nameof(OrganizationStatus.Active).ToLowerInvariant(),
        nameof(OrganizationStatus.Suspended).ToLowerInvariant(),
        nameof(OrganizationStatus.Archived).ToLowerInvariant()
    ];

    public UpdateOrganizationStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Allowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status must be active, suspended, or archived.");
    }
}
