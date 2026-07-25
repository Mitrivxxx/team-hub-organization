using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TeamHub.BlobStorage;
using team_hub_organization.Controllers;
using team_hub_organization.Controllers.Me;
using team_hub_organization.Controllers.Members.AllMembers;
using team_hub_organization.Controllers.Members.Invitations;
using team_hub_organization.Controllers.Members.Permissions;
using team_hub_organization.Controllers.Members.Roles;
using team_hub_organization.Data;
using team_hub_organization.Models;
using team_hub_organization.Services;
using team_hub_organization.Services.Me;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Members.Invitations;
using team_hub_organization.Services.Members.Permissions;
using team_hub_organization.Services.Members.Roles;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;
using team_hub_organization.Services.Teams;

namespace team_hub_organization.Tests.Controllers;

internal static class OrganizationsControllerTestHelpers
{
    public static OrganizationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"organization-tests-{Guid.NewGuid():N}")
            .Options;

        var db = new OrganizationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static OrganizationsController CreateController(
        OrganizationDbContext db,
        Guid userId,
        IOrganizationService? organizationService = null,
        IOrganizationAvatarService? organizationAvatarService = null,
        IBlobStorageService? blobStorageService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreatePrincipal(userId)
        };

        var currentUserService = new TestCurrentUserService(userId);
        var authz = new OrganizationAuthorizationService(db);
        var serviceProvider = CreateServiceProvider(blobStorageService);
        organizationService ??= new OrganizationService(db, authz, serviceProvider);
        organizationAvatarService ??= new OrganizationAvatarService(db, authz, serviceProvider);

        return new OrganizationsController(organizationService, organizationAvatarService, currentUserService, NullLogger<OrganizationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    public static MembersController CreateMembersController(OrganizationDbContext db, Guid userId) =>
        new(new MemberService(db, new OrganizationAuthorizationService(db)), new TestCurrentUserService(userId));

    public static RolesController CreateRolesController(OrganizationDbContext db, Guid userId) =>
        new(new RoleService(db, new OrganizationAuthorizationService(db)), new TestCurrentUserService(userId));

    public static PermissionsController CreatePermissionsController(OrganizationDbContext db) =>
        new(new PermissionService(db));

    public static TeamsController CreateTeamsController(OrganizationDbContext db, Guid userId, IBlobStorageService? blob = null)
    {
        var authz = new OrganizationAuthorizationService(db);
        var sp = CreateServiceProvider(blob);
        return new TeamsController(
            new TeamService(db, authz, sp),
            new TeamAvatarService(db, authz, sp),
            new TestCurrentUserService(userId));
    }

    public static InvitationsController CreateInvitationsController(OrganizationDbContext db, Guid userId, string? email = null)
    {
        var httpContext = new DefaultHttpContext { User = CreatePrincipal(userId, email) };
        return new InvitationsController(
            new InvitationService(db, new OrganizationAuthorizationService(db)),
            new TestCurrentUserService(userId))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    public static MeController CreateMeController(OrganizationDbContext db, Guid userId)
    {
        var authz = new OrganizationAuthorizationService(db);
        var members = new MemberService(db, authz);
        return new MeController(new MeService(db, authz, members), new TestCurrentUserService(userId));
    }

    public static OrganizationService CreateOrganizationService(
        OrganizationDbContext db,
        IBlobStorageService? blobStorageService = null) =>
        new(db, new OrganizationAuthorizationService(db), CreateServiceProvider(blobStorageService));

    public static IOrganizationAvatarService CreateAvatarService(
        OrganizationDbContext db,
        IBlobStorageService? blobStorageService = null) =>
        new OrganizationAvatarService(db, new OrganizationAuthorizationService(db), CreateServiceProvider(blobStorageService));

    static IServiceProvider CreateServiceProvider(IBlobStorageService? blobStorageService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(blobStorageService ?? new FakeBlobStorageService());
        return services.BuildServiceProvider();
    }

    public static ClaimsPrincipal CreatePrincipal(Guid userId, string? email = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    public static async Task<(Organization organization, Role ownerRole, OrganizationMember member, OrganizationRoleSeeder.SeededRoles roles)> SeedOrganizationAsync(
        OrganizationDbContext db,
        Guid userId,
        string name = "Acme",
        string slug = "acme")
    {
        var now = DateTimeOffset.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Organizations.Add(organization);
        var roles = await OrganizationRoleSeeder.SeedSystemRolesAsync(db, organization.Id, now);

        var member = new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = userId,
            RoleId = roles.Owner.Id,
            JoinedAt = now
        };

        db.OrganizationMembers.Add(member);
        await db.SaveChangesAsync();

        return (organization, roles.Owner, member, roles);
    }

    sealed class TestCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid GetRequiredUserId() => userId;

        public bool TryGetUserId(out Guid resolvedUserId)
        {
            resolvedUserId = userId;
            return true;
        }
    }
}
