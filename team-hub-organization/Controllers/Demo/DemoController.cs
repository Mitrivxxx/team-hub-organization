using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Controllers;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Demo;

namespace team_hub_organization.Controllers.Demo;

public sealed class DemoController(
    IDemoSeedContextService demoSeedContext,
    IHostEnvironment environment) : OrganizationApiController
{
    /// <summary>Demo seed ids for Swagger try-out (Development/Staging only).</summary>
    [HttpGet("demo/context")]
    [ProducesResponseType(typeof(DemoSeedContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContext(
        [FromQuery] int index = 1,
        CancellationToken cancellationToken = default)
    {
        if (environment.IsProduction()
            || (!environment.IsDevelopment() && !environment.IsEnvironment("Staging")))
            return NotFound();

        var context = await demoSeedContext.GetAsync(index, cancellationToken);
        return context is null ? NotFound() : Ok(context);
    }
}
