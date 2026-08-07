using TeamHub.Observability;
using TeamHub.Observability.Middleware;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Configuration;

public sealed class OrganizationExceptionMapper : IExceptionProblemDetailsMapper
{
    public bool TryMap(Exception exception, out ExceptionMapping mapping)
    {
        switch (exception)
        {
            case OrganizationNotFoundException ex:
                mapping = new ExceptionMapping(
                    StatusCodes.Status404NotFound,
                    "Not found.",
                    ex.Message,
                    ProblemTypes.For("organization-not-found"),
                    PreferMappedDetail: true);
                return true;

            case OrganizationAccessException ex:
                mapping = new ExceptionMapping(
                    StatusCodes.Status403Forbidden,
                    "Forbidden.",
                    ex.Message,
                    ProblemTypes.Forbidden,
                    PreferMappedDetail: true);
                return true;

            case OrganizationConflictException ex:
                mapping = new ExceptionMapping(
                    StatusCodes.Status409Conflict,
                    "Conflict.",
                    ex.Message,
                    ProblemTypes.Conflict,
                    PreferMappedDetail: true);
                return true;

            case OrganizationQuotaExceededException ex:
                mapping = new ExceptionMapping(
                    StatusCodes.Status409Conflict,
                    "Quota exceeded.",
                    ex.Message,
                    ProblemTypes.For("quota-exceeded"),
                    PreferMappedDetail: true);
                return true;

            case OrganizationGoneException ex:
                mapping = new ExceptionMapping(
                    StatusCodes.Status410Gone,
                    "Gone.",
                    ex.Message,
                    ProblemTypes.For("organization-gone"),
                    PreferMappedDetail: true);
                return true;

            case OrganizationValidationException or OrganizationAvatarValidationException:
                mapping = new ExceptionMapping(
                    StatusCodes.Status400BadRequest,
                    "Bad request.",
                    exception.Message,
                    ProblemTypes.ValidationFailed,
                    PreferMappedDetail: true);
                return true;

            case OrganizationAvatarStorageUnavailableException:
                mapping = new ExceptionMapping(
                    StatusCodes.Status503ServiceUnavailable,
                    "Blob storage is not configured.",
                    exception.Message,
                    ProblemTypes.ServiceUnavailable,
                    PreferMappedDetail: true);
                return true;

            default:
                mapping = default;
                return false;
        }
    }
}
