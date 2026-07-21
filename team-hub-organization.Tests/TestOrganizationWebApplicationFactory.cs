using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Tests.Configuration;
using team_hub_organization.Tests.Controllers;

namespace team_hub_organization.Tests;

public sealed class TestOrganizationWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5433;Database=organization_test;Username=matsma");
        builder.UseSetting("Jwt:Key", TestJwtConfiguration.Key);
        builder.UseSetting("Jwt:Issuer", TestJwtConfiguration.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwtConfiguration.Audience);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrganizationDbContext>>();
            services.RemoveAll<OrganizationDbContext>();

            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase("team-hub-organization-tests"));

            services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
            services.AddHealthChecks();
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, FakeBlobStorageService>();
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(userId));
        return client;
    }

    public static string CreateAccessToken(Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtConfiguration.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtConfiguration.Issuer,
            audience: TestJwtConfiguration.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, "test-user")
            ],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class OrganizationsIntegrationTests : IClassFixture<TestOrganizationWebApplicationFactory>
{
    readonly TestOrganizationWebApplicationFactory _factory;

    public OrganizationsIntegrationTests(TestOrganizationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithoutToken_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/organizations", new CreateOrganizationRequest { Name = "Acme" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithToken_ShouldReturnCreated()
    {
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/organizations", new CreateOrganizationRequest { Name = "Acme" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var organization = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(organization);
        Assert.Equal("acme", organization.Slug);
    }
}
