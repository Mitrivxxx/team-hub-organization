namespace team_hub_organization.Dtos;

public sealed class UpdateOrganizationRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Nip { get; set; }
    public OrganizationAddressDto? Address { get; set; }
}
