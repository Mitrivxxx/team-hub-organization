using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using team_hub_organization.Configuration;

namespace team_hub_organization.Controllers;

[ApiController]
[ApiVersion(OrganizationApiVersions.Current)]
[Route("api/organizations/v{version:apiVersion}")]
[Authorize]
public abstract class OrganizationApiController : ControllerBase
{
    protected CreatedAtActionResult CreatedAtVersionedAction(string actionName, object routeValues, object? value)
    {
        var values = new RouteValueDictionary(routeValues)
        {
            ["version"] = OrganizationApiVersions.VersionMajor
        };
        return CreatedAtAction(actionName, values, value);
    }

    protected AcceptedAtActionResult AcceptedAtVersionedAction(string actionName, object routeValues, object? value)
    {
        var values = new RouteValueDictionary(routeValues)
        {
            ["version"] = OrganizationApiVersions.VersionMajor
        };
        return AcceptedAtAction(actionName, values, value);
    }
}
