namespace team_hub_organization.Services.Auth;

public interface IAuthUserResolveClient
{
    Task<IReadOnlyDictionary<Guid, AuthUserProfile>> GetUsersByIdsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<AuthResolveUsersResult> ResolveUsersAsync(
        IReadOnlyList<string> emails,
        IReadOnlyList<string> usernames,
        CancellationToken cancellationToken = default);
}

public sealed record AuthUserProfile(
    Guid Id,
    string Username,
    string Email,
    string Name,
    string Surname);

public sealed record AuthResolveUsersResult(
    IReadOnlyList<AuthUserProfile> Users,
    IReadOnlyList<string> UnresolvedEmails,
    IReadOnlyList<string> UnresolvedUsernames);
