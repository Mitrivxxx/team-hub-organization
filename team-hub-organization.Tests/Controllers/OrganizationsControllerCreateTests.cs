using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerCreateTests
{
    [Fact]
    public async Task Create_WhenRequestIsValid_ShouldReturnCreatedAndPersistOwnerMember()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.Create(new CreateOrganizationRequest { Name = "Acme Corp" }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<OrganizationResponse>(created.Value);
        Assert.Equal("Acme Corp", response.Name);
        Assert.Equal("acme-corp", response.Slug);

        var member = await db.OrganizationMembers
            .Include(m => m.Role)
            .SingleAsync(m => m.UserId == userId);

        Assert.Equal("Owner", member.Role.Name);
    }

    [Fact]
    public async Task Create_WhenSlugAlreadyExists_ShouldReturnConflict()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, Guid.NewGuid(), slug: "acme");
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.Create(new CreateOrganizationRequest
        {
            Name = "Acme",
            Slug = "acme"
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
