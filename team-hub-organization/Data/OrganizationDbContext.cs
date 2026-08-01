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
    public DbSet<OrganizationMemberRole> OrganizationMemberRoles => Set<OrganizationMemberRole>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<InvitationOrgRole> InvitationOrgRoles => Set<InvitationOrgRole>();
    public DbSet<OrganizationActivity> OrganizationActivities => Set<OrganizationActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.HasIndex(o => o.Slug).IsUnique();
            e.HasIndex(o => o.Email).IsUnique().HasFilter("\"Email\" <> ''");
            e.Property(o => o.Name).HasMaxLength(100);
            e.Property(o => o.Slug).HasMaxLength(100);
            e.Property(o => o.Description).HasMaxLength(500);
            e.Property(o => o.Nip).HasMaxLength(20);
            e.Property(o => o.Email).HasMaxLength(255);
            e.OwnsOne(o => o.Address, a =>
            {
                a.Property(x => x.Country).HasColumnName("Country").HasMaxLength(100);
                a.Property(x => x.City).HasColumnName("City").HasMaxLength(100);
                a.Property(x => x.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
            });
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasIndex(p => new { p.OrganizationId, p.Code }).IsUnique();
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.Code).HasMaxLength(100);
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasOne(p => p.Organization).WithMany(o => o.Permissions).HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasIndex(r => r.OrganizationId);
            e.HasIndex(r => new { r.OrganizationId, r.Name, r.Scope }).IsUnique();
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Description).HasMaxLength(500);
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
            e.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrganizationMember>(e =>
        {
            e.ToTable("organization_members");
            e.HasKey(m => new { m.OrganizationId, m.UserId });
            e.HasIndex(m => m.UserId);
            e.HasOne(m => m.Organization).WithMany(o => o.Members).HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizationMemberRole>(e =>
        {
            e.ToTable("organization_member_roles");
            e.HasKey(m => new { m.OrganizationId, m.UserId, m.RoleId });
            e.HasIndex(m => m.RoleId);
            e.HasOne(m => m.Member)
                .WithMany(member => member.MemberRoles)
                .HasForeignKey(m => new { m.OrganizationId, m.UserId })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Role)
                .WithMany(r => r.OrganizationMemberRoles)
                .HasForeignKey(m => m.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
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
            e.HasOne(i => i.TeamRole).WithMany(r => r.TeamInvitations).HasForeignKey(i => i.TeamRoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvitationOrgRole>(e =>
        {
            e.ToTable("invitation_org_roles");
            e.HasKey(x => new { x.InvitationId, x.RoleId });
            e.HasOne(x => x.Invitation).WithMany(i => i.OrgRoles).HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.InvitationOrgRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrganizationActivity>(e =>
        {
            e.ToTable("organization_activities");
            e.HasIndex(a => new { a.OrganizationId, a.OccurredAt });
            e.HasIndex(a => new { a.OrganizationId, a.Type, a.OccurredAt });
            e.Property(a => a.Type).HasMaxLength(64);
            e.Property(a => a.EntityType).HasMaxLength(64);
            e.Property(a => a.Details).HasColumnType("jsonb");
            e.HasOne(a => a.Organization).WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
