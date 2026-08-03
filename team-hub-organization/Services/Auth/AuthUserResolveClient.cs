using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using TeamHub.GrpcContracts.Auth.V1;
using team_hub_organization.Configuration.Options;

namespace team_hub_organization.Services.Auth;

public sealed class AuthUserResolveClient : IAuthUserResolveClient, IDisposable
{
    readonly GrpcChannel _channel;
    readonly UserProfileService.UserProfileServiceClient _client;

    public AuthUserResolveClient(IOptions<GrpcOptions> options)
    {
        _channel = GrpcChannel.ForAddress(options.Value.Auth);
        _client = new UserProfileService.UserProfileServiceClient(_channel);
    }

    public async Task<IReadOnlyDictionary<Guid, AuthUserProfile>> GetUsersByIdsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, AuthUserProfile>();

        var request = new GetUsersByIdsRequest();
        request.UserIds.AddRange(userIds.Select(id => id.ToString()));

        var response = await _client.GetUsersByIdsAsync(request, cancellationToken: cancellationToken);

        return response.Users
            .Where(u => Guid.TryParse(u.Id, out _))
            .ToDictionary(
                u => Guid.Parse(u.Id),
                MapProfile);
    }

    public async Task<AuthResolveUsersResult> ResolveUsersAsync(
        IReadOnlyList<string> emails,
        IReadOnlyList<string> usernames,
        CancellationToken cancellationToken = default)
    {
        var request = new ResolveUsersRequest();
        request.Emails.AddRange(emails);
        request.Usernames.AddRange(usernames);

        var response = await _client.ResolveUsersAsync(request, cancellationToken: cancellationToken);

        var users = response.Users
            .Where(u => Guid.TryParse(u.Id, out _))
            .Select(MapProfile)
            .ToList();

        return new AuthResolveUsersResult(
            users,
            response.UnresolvedEmails.ToList(),
            response.UnresolvedUsernames.ToList());
    }

    static AuthUserProfile MapProfile(UserProfile u) =>
        new(Guid.Parse(u.Id), u.Username, u.Email, u.Name, u.Surname);

    public void Dispose() => _channel.Dispose();
}
