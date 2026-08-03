using System.ComponentModel.DataAnnotations;

namespace team_hub_organization.Configuration.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Key { get; set; } = "";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";
}
