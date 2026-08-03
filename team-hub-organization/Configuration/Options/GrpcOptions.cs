using System.ComponentModel.DataAnnotations;

namespace team_hub_organization.Configuration.Options;

public sealed class GrpcOptions
{
    public const string SectionName = "Grpc";

    [Required]
    public string Auth { get; set; } = "";
}
