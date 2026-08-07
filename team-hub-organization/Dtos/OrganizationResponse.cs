namespace team_hub_organization.Dtos;

public sealed class OrganizationResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string Nip { get; set; } = "";
    public string Email { get; set; } = "";
    public OrganizationAddressDto Address { get; set; } = new();
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
