using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using team_hub_organization.Data;

namespace team_hub_organization.Tests;

public sealed class TestOrganizationWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5433;Database=organization_test;Username=matsma");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrganizationDbContext>>();
            services.RemoveAll<OrganizationDbContext>();

            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase("team-hub-organization-tests"));

            services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
            services.AddHealthChecks();
        });
    }
}
