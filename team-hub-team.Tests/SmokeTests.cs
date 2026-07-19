using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace team_hub_team.Tests;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
