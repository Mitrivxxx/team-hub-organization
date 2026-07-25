using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

internal static class ControllerExceptionMapper
{
    public static IActionResult Map(Exception ex) => ex switch
    {
        OrganizationNotFoundException => new NotFoundObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not found.",
            Detail = ex.Message
        }),
        OrganizationAccessException => new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden.",
            Detail = ex.Message
        })
        { StatusCode = StatusCodes.Status403Forbidden },
        OrganizationConflictException => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict.",
            Detail = ex.Message
        }),
        OrganizationValidationException => new BadRequestObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad request.",
            Detail = ex.Message
        }),
        _ => throw ex
    };
}
