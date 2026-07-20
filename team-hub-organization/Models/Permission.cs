namespace team_hub_organization.Models;

public class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
