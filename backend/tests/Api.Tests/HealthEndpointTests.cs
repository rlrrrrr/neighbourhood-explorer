using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NeighbourhoodExplorer.Api;

namespace NeighbourhoodExplorer.Api.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_reports_disabled_ai_and_missing_database_when_not_configured()
    {
        using var factory = CreateFactory(aiEnabled: "false");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("unavailable", health.Database);
        Assert.Equal("disabled", health.Ai);
        Assert.Null(health.DataVersion);
    }

    [Fact]
    public async Task Health_reports_unavailable_ai_when_enabled_without_model()
    {
        using var factory = CreateFactory(aiEnabled: "true");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("unavailable", health.Ai);
    }

    private static WebApplicationFactory<Program> CreateFactory(string aiEnabled) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("AI_ENABLED", aiEnabled)
            .UseSetting("DATABASE_CONNECTION_STRING", ""));
}
