using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerAvatarTests
{
    [Fact]
    public async Task UploadAvatar_WhenUserIsMember_ShouldStoreBlobAndReturnSasUrl()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var blobStorage = new FakeBlobStorageService();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId, blobStorageService: blobStorage);

        await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var file = new FormFile(stream, 0, stream.Length, "file", "avatar.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.UploadAvatar(organization.Id, file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationResponse>(ok.Value);
        Assert.Equal($"https://blob.test/organizations/{organization.Id}/avatar.jpg?sas=fake", response.AvatarUrl);

        var persisted = await db.Organizations.SingleAsync(o => o.Id == organization.Id);
        Assert.Equal($"organizations/{organization.Id}/avatar.jpg", persisted.AvatarUrl);
        Assert.True(blobStorage.Contains(persisted.AvatarUrl!));
    }

    [Fact]
    public async Task UploadAvatar_WhenInvalidContentType_ShouldReturnBadRequest()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        await using var stream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(stream, 0, stream.Length, "file", "avatar.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        await Assert.ThrowsAsync<OrganizationAvatarValidationException>(() =>
            controller.UploadAvatar(organization.Id, file, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAvatar_WhenUserIsMember_ShouldClearAvatar()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var blobStorage = new FakeBlobStorageService();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        organization.AvatarUrl = $"organizations/{organization.Id}/avatar.png";
        await db.SaveChangesAsync();
        await blobStorage.UploadAsync(organization.AvatarUrl, new MemoryStream([1, 2, 3]), "image/png");

        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId, blobStorageService: blobStorage);
        var result = await controller.DeleteAvatar(organization.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationResponse>(ok.Value);
        Assert.Null(response.AvatarUrl);

        var persisted = await db.Organizations.SingleAsync(o => o.Id == organization.Id);
        Assert.Null(persisted.AvatarUrl);
        Assert.False(blobStorage.Contains($"organizations/{organization.Id}/avatar.png"));
    }
}
