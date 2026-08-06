using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerDeleteTests
{
    [Fact]
    public async Task Delete_WhenUserIsOwner_ShouldSoftDeleteOrganization()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.Delete(organization.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        var persisted = await db.Organizations.SingleAsync(o => o.Id == organization.Id);
        Assert.NotNull(persisted.DeletedAt);
    }

    [Fact]
    public async Task Delete_WhenUserIsNotOwner_ShouldReturnForbidden()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateController(db, memberId);
        await Assert.ThrowsAsync<OrganizationAccessException>(() =>
            controller.Delete(organization.Id, CancellationToken.None));
    }
}
