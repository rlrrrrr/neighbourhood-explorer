using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using NeighbourhoodExplorer.Api.Features.Metrics;

namespace NeighbourhoodExplorer.Api.Tests;

public class MetricsEndpointTests(MetricsApiFixture fixture) : IClassFixture<MetricsApiFixture>
{
    [Fact]
    public async Task Metrics_lists_definitions_with_source_translations_and_latest_date()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MetricsResponse>();
        Assert.NotNull(payload);

        var metric = Assert.Single(payload.Metrics);
        Assert.Equal("population_density", metric.Key);
        Assert.Equal("habitants/km²", metric.Unit);
        Assert.Equal("neighborhood", metric.NativeLevel);
        Assert.Equal("neutral", metric.Polarity);
        Assert.Equal("percentile", metric.Normalization.Method);
        Assert.Equal("percentile-v1", metric.Normalization.Version);
        Assert.Equal(0, metric.DisplayPrecision);
        Assert.Equal(new DateOnly(2025, 12, 31), metric.ObservedAt);

        Assert.Equal("worldpop-test", metric.Source.Id);
        Assert.Equal("WorldPop", metric.Source.Organization);
        Assert.Equal("CC-BY-4.0", metric.Source.License);
        Assert.Equal("WorldPop (test)", metric.Source.Attribution);
        Assert.Equal(new DateOnly(2026, 1, 15), metric.Source.RetrievedAt);

        Assert.Collection(
            metric.Translations,
            translation =>
            {
                Assert.Equal("en", translation.Locale);
                Assert.Equal("Population density", translation.Name);
            },
            translation =>
            {
                Assert.Equal("fr", translation.Locale);
                Assert.Equal("Densité de population", translation.Name);
            });
    }

    [Fact]
    public async Task Metric_values_resolve_the_city_by_official_code_and_keep_the_latest_observation()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities/34/metric-values?metrics=population_density");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content
            .ReadFromJsonAsync<Dictionary<string, Dictionary<string, MetricValueResponse>>>();
        Assert.NotNull(payload);

        Assert.True(payload.TryGetValue("population_density", out var values));
        Assert.NotNull(values);
        Assert.Equal(2, values.Count);

        var moda = values[fixture.ModaNeighborhoodId];
        Assert.Equal(200, moda.Raw);
        Assert.Equal(0.5, moda.Normalized);
        Assert.Equal(QualityFlag.Estimated, moda.QualityFlag);

        var fenerbahce = values[fixture.FenerbahceNeighborhoodId];
        Assert.Equal(300, fenerbahce.Raw);
        Assert.Equal(0.75, fenerbahce.Normalized);

        Assert.DoesNotContain(fixture.KizilayNeighborhoodId, values.Keys);
    }

    [Fact]
    public async Task Metric_values_return_an_empty_object_for_a_metric_without_values()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities/6/metric-values?metrics=population_density");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content
            .ReadFromJsonAsync<Dictionary<string, Dictionary<string, MetricValueResponse>>>();
        Assert.NotNull(payload);

        Assert.True(payload.TryGetValue("population_density", out var values));
        Assert.NotNull(values);
        var kizilay = Assert.Single(values);
        Assert.Equal(fixture.KizilayNeighborhoodId, kizilay.Key);
        Assert.Equal(999, kizilay.Value.Raw);
    }

    [Fact]
    public async Task Metric_values_reject_an_unknown_metric()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/v1/cities/34/metric-values?metrics=population_density,unknown_metric");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("unknown_metric", problem.Extensions["code"]?.ToString());
        Assert.Contains("unknown_metric", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Metric_values_require_the_metrics_parameter()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities/34/metric-values");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("metrics_required", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Metric_values_return_404_for_an_unknown_city()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities/99/metric-values?metrics=population_density");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("city_not_found", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Metrics_support_etag_revalidation()
    {
        using var client = fixture.Factory.CreateClient();
        using var first = await client.GetAsync("/api/v1/metrics");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        Assert.False(string.IsNullOrWhiteSpace(first.Headers.CacheControl?.ToString()));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/metrics");
        request.Headers.IfNoneMatch.Add(etag);
        using var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(etag, second.Headers.ETag);
    }

    [Fact]
    public async Task Metrics_return_503_when_the_database_is_not_configured()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("AI_ENABLED", "false")
            .UseSetting("DATABASE_CONNECTION_STRING", ""));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/metrics");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("database_unavailable", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Metrics_return_503_when_the_database_is_unreachable()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("AI_ENABLED", "false")
            .UseSetting(
                "DATABASE_CONNECTION_STRING",
                "Host=127.0.0.1;Port=1;Database=explorer;Username=explorer;Password=explorer;" +
                "Timeout=1;Command Timeout=1"));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/metrics");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("database_unavailable", problem.Extensions["code"]?.ToString());
    }
}
