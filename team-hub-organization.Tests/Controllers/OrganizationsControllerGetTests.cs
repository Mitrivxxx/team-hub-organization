using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerGetTests
{
    [Fact]
    public async Task GetById_WhenUserIsMember_ShouldReturnOrganization()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.GetById(organization.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationResponse>(ok.Value);
        Assert.Equal(organization.Id, response.Id);
    }

    [Fact]
    public async Task GetById_WhenUserIsNotMember_ShouldReturnForbidden()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, Guid.NewGuid());
        var controller = OrganizationsControllerTestHelpers.CreateController(db, Guid.NewGuid());

        await Assert.ThrowsAsync<OrganizationAccessException>(() =>
            controller.GetById(organization.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetBySlug_WhenUserIsMember_ShouldReturnOrganization()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId, slug: "acme");
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.GetBySlug("acme", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationResponse>(ok.Value);
        Assert.Equal(organization.Id, response.Id);
    }

    [Fact]
    public async Task GetById_WhenOrganizationIsDeleted_ShouldReturnNotFound()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        organization.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);
        var result = await controller.GetById(organization.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
