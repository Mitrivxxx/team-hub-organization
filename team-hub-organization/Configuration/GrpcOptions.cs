namespace team_hub_organization.Configuration;

public sealed class GrpcOptions
{
    public const string SectionName = "Grpc";

    public string Auth { get; set; } = "http://localhost:5101";
}
