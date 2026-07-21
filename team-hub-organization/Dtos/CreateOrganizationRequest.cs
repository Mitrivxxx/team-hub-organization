namespace team_hub_organization.Dtos;

public sealed class CreateOrganizationRequest
{
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public string? Description { get; set; }
}
