using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Serilog.Context;

namespace team_hub_organization.Configuration;

public sealed class UserIdLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        using (LogContext.PushProperty("UserId", userId))
        {
            await next(context);
        }
    }
}
