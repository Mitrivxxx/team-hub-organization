using Microsoft.Extensions.Options;
using team_hub_organization.Configuration;

namespace team_hub_organization.Tests.Configuration;

internal static class TestJwtConfiguration
{
    public const string Key = "test-secret-key-at-least-32-characters-long";
    public const string Issuer = "AuthService";
    public const string Audience = "AuthServiceUsers";

    public static IOptions<JwtOptions> CreateJwtOptions() =>
        Options.Create(new JwtOptions
        {
            Key = Key,
            Issuer = Issuer,
            Audience = Audience
        });
}
