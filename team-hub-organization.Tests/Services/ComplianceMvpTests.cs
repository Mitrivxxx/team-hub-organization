using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Members.Audit;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;
using team_hub_organization.Tests.Controllers;

namespace team_hub_organization.Tests.Services;

public class ComplianceMvpTests
{
    [Fact]
    public async Task CreateOrganization_WhenQuotaExceeded_Throws()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId, "One", "one");
        await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId, "Two", "two");

        var quotas = Options.Create(new OrganizationQuotasOptions { MaxOrgsPerUser = 2 });
        var service = CreateOrgService(db, quotas);

        await Assert.ThrowsAsync<OrganizationQuotaExceededException>(() =>
            service.CreateAsync(
                new CreateOrganizationRequest
                {
                    Name = "Three",
                    Nip = "1234567890",
                    Address = new OrganizationAddressDto
                    {
                        Country = "Poland",
                        City = "Warsaw",
                        PostalCode = "00-001"
                    }
                },
                userId));
    }

    [Fact]
    public async Task TransferOwnership_WritesAuditEvent()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Admin.Id);
        await db.SaveChangesAsync();

        var service = CreateOrgService(db);
        await service.TransferOwnershipAsync(organization.Id, ownerId, memberId);

        var audit = await db.OrganizationAuditEvents.AsNoTracking()
            .SingleAsync(a => a.OrganizationId == organization.Id && a.Action == AuditActions.OwnershipTransferred);
        Assert.Equal(ownerId, audit.ActorUserId);
        Assert.Equal(memberId, audit.TargetUserId);
    }

    [Fact]
    public async Task LifecycleOptions_BindDefaults()
    {
        var options = new OrganizationLifecycleOptions();
        Assert.Equal(30, options.RetentionDays);
        Assert.Equal(90, options.ActivityTtlDays);
        Assert.Equal(14, options.ImportArtifactTtlDays);
        Assert.Equal(24, options.IdempotencyTtlHours);
    }

    static OrganizationService CreateOrgService(
        OrganizationDbContext db,
        IOptions<OrganizationQuotasOptions>? quotas = null)
    {
        var httpContext = new DefaultHttpContext();
        return new OrganizationService(
            db,
            new OrganizationAuthorizationService(db),
            new ActivityRecorder(db),
            new AuditRecorder(db, new HttpContextAccessor { HttpContext = httpContext }),
            new NoOpOrganizationLifecycleNotifier(),
            Options.Create(new OrganizationLifecycleOptions()),
            quotas ?? Options.Create(new OrganizationQuotasOptions()),
            new ServiceCollection().BuildServiceProvider());
    }
}
