using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace team_hub_organization.Services;

public interface ICurrentUserService
{
    Guid GetRequiredUserId();
    bool TryGetUserId(out Guid userId);
}

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid GetRequiredUserId()
    {
        if (TryGetUserId(out var userId))
            return userId;

        throw new UnauthorizedAccessException("User is not authenticated.");
    }

    public bool TryGetUserId(out Guid userId)
    {
        userId = default;
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal is null)
            return false;

        var rawUserId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrWhiteSpace(rawUserId) && Guid.TryParse(rawUserId, out userId);
    }
}
