using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NeighbourhoodExplorer.Api;

namespace NeighbourhoodExplorer.Api.Tests;

public class ResponseCompressionTests
{
    [Fact]
    public async Task Compresses_json_when_the_client_accepts_gzip()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("gzip", Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task Does_not_compress_when_the_client_does_not_accept_it()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("AI_ENABLED", "false")
            .UseSetting("DATABASE_CONNECTION_STRING", ""));
}
