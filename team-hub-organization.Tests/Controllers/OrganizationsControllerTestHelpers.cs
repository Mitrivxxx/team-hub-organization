using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using team_hub_organization.Controllers;
using team_hub_organization.Data;
using team_hub_organization.Models;
using team_hub_organization.Services;
using team_hub_organization.Services.Organizations;

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
        IOrganizationService? organizationService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreatePrincipal(userId)
        };

        var currentUserService = new TestCurrentUserService(userId);
        organizationService ??= new OrganizationService(db);

        return new OrganizationsController(organizationService, currentUserService, NullLogger<OrganizationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    public static ClaimsPrincipal CreatePrincipal(Guid userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        ],
        authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }

    public static async Task<(Organization organization, Role ownerRole, OrganizationMember member)> SeedOrganizationAsync(
        OrganizationDbContext db,
        Guid userId,
        string name = "Acme",
        string slug = "acme",
        string roleName = OrganizationService.OwnerRoleName)
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

        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = roleName,
            Scope = RoleScope.Org,
            CreatedAt = now
        };

        var member = new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = userId,
            RoleId = role.Id,
            JoinedAt = now
        };

        db.Organizations.Add(organization);
        db.Roles.Add(role);
        db.OrganizationMembers.Add(member);
        await db.SaveChangesAsync();

        return (organization, role, member);
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
