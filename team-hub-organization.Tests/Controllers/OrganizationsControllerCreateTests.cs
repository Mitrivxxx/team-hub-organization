using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerCreateTests
{
    [Fact]
    public async Task Create_WhenRequestIsValid_ShouldReturnCreatedAndPersistOwnerMember()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.Create(new CreateOrganizationRequest
        {
            Name = "Acme Corp",
            Nip = "1234567890",
            Address = new OrganizationAddressDto
            {
                Country = "Poland",
                City = "Warsaw",
                PostalCode = "00-001"
            }
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<OrganizationResponse>(created.Value);
        Assert.Equal("Acme Corp", response.Name);
        Assert.Equal("acme-corp", response.Slug);
        Assert.Equal("1234567890", response.Nip);
        Assert.Equal("Poland", response.Address.Country);
        Assert.Equal("Warsaw", response.Address.City);
        Assert.Equal("00-001", response.Address.PostalCode);
        Assert.Matches(@"^acmecorp\d{4}@teamhub\.local$", response.Email);

        var member = await db.OrganizationMembers
            .SingleAsync(m => m.UserId == userId);

        var ownerRole = await db.OrganizationMemberRoles
            .Where(m => m.OrganizationId == member.OrganizationId && m.UserId == userId)
            .Join(db.Roles, m => m.RoleId, r => r.Id, (_, r) => r)
            .SingleAsync(r => r.Name == "Owner");

        Assert.Equal("Owner", ownerRole.Name);
    }

    [Fact]
    public async Task Create_WhenSlugAlreadyExists_ShouldReturnConflict()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, Guid.NewGuid(), slug: "acme");
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        await Assert.ThrowsAsync<OrganizationConflictException>(() =>
            controller.Create(new CreateOrganizationRequest
            {
                Name = "Acme",
                Slug = "acme",
                Nip = "1234567890",
                Address = new OrganizationAddressDto
                {
                    Country = "Poland",
                    City = "Warsaw",
                    PostalCode = "00-001"
                }
            }, CancellationToken.None));
    }
}
