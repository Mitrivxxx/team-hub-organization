using Microsoft.EntityFrameworkCore;
using team_hub_organization.Models;

namespace team_hub_organization.Data;

public class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.HasIndex(o => o.Slug).IsUnique();
            e.Property(o => o.Name).HasMaxLength(100);
            e.Property(o => o.Slug).HasMaxLength(100);
            e.Property(o => o.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(100);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasIndex(r => r.OrganizationId);
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Scope)
                .HasConversion(v => v == RoleScope.Org ? "ORG" : "TEAM", v => v == "ORG" ? RoleScope.Org : RoleScope.Team)
                .HasMaxLength(20);
            e.HasOne(r => r.Organization).WithMany(o => o.Roles).HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizationMember>(e =>
        {
            e.ToTable("organization_members");
            e.HasKey(m => new { m.OrganizationId, m.UserId });
            e.HasIndex(m => m.UserId);
            e.HasOne(m => m.Organization).WithMany(o => o.Members).HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Role).WithMany(r => r.OrganizationMembers).HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.ToTable("teams");
            e.HasIndex(t => t.OrganizationId);
            e.Property(t => t.Name).HasMaxLength(100);
            e.HasOne(t => t.Organization).WithMany(o => o.Teams).HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMember>(e =>
        {
            e.ToTable("team_members");
            e.HasKey(m => new { m.TeamId, m.UserId });
            e.HasIndex(m => m.UserId);
            e.Property(m => m.JobTitle).HasMaxLength(100);
            e.HasOne(m => m.Team).WithMany(t => t.Members).HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Role).WithMany(r => r.TeamMembers).HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invitation>(e =>
        {
            e.ToTable("invitations");
            e.HasIndex(i => i.Token).IsUnique();
            e.HasIndex(i => new { i.OrganizationId, i.Email, i.Status });
            e.Property(i => i.Email).HasMaxLength(255);
            e.Property(i => i.Token).HasMaxLength(255);
            e.Property(i => i.Status)
                .HasConversion(v => v.ToString().ToUpperInvariant(), v => Enum.Parse<InvitationStatus>(v, true))
                .HasMaxLength(20);
            e.HasOne(i => i.Organization).WithMany(o => o.Invitations).HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Team).WithMany(t => t.Invitations).HasForeignKey(i => i.TeamId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.OrgRole).WithMany(r => r.OrganizationInvitations).HasForeignKey(i => i.OrgRoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.TeamRole).WithMany(r => r.TeamInvitations).HasForeignKey(i => i.TeamRoleId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
