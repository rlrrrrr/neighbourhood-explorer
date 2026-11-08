using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using NeighbourhoodExplorer.Api.Features.Geography;
using NeighbourhoodExplorer.Api.Features.Metrics;

namespace NeighbourhoodExplorer.Api.Tests;

public class GeographyEndpointTests(MetricsApiFixture fixture) : IClassFixture<MetricsApiFixture>
{
    [Fact]
    public async Task Cities_list_provinces_by_official_code()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CitiesResponse>();
        Assert.NotNull(payload);

        Assert.Collection(
            payload.Cities,
            ankara =>
            {
                Assert.Equal(6, ankara.PlateCode);
                Assert.Equal("Ankara", ankara.Name);
                Assert.Equal("TR-06", ankara.OfficialCode);
            },
            istanbul =>
            {
                Assert.Equal(34, istanbul.PlateCode);
                Assert.Equal("İstanbul", istanbul.Name);
                Assert.Equal("TR-34", istanbul.OfficialCode);
            });
    }

    [Fact]
    public async Task Neighborhoods_return_simplified_geojson_for_the_requested_city()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities/34/neighborhoods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/geo+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromDays(1), response.Headers.CacheControl?.MaxAge);

        var payload = await response.Content.ReadFromJsonAsync<NeighborhoodFeatureCollection>();
        Assert.NotNull(payload);
        Assert.Equal("FeatureCollection", payload.Type);
        Assert.Equal(2, payload.Features.Count);

        var moda = Assert.Single(
            payload.Features,
            feature => feature.Properties.Id.ToString() == fixture.ModaNeighborhoodId);
        Assert.Equal("Feature", moda.Type);
        Assert.Equal("Moda Mahallesi", moda.Properties.Name);
        Assert.Equal("Kadıköy", moda.Properties.DistrictName);
        Assert.Contains(
            moda.Geometry.GetProperty("type").GetString(),
            new[] { "Polygon", "MultiPolygon" });

        var fenerbahce = Assert.Single(
            payload.Features,
            feature => feature.Properties.Id.ToString() == fixture.FenerbahceNeighborhoodId);
        Assert.Equal("Fenerbahçe Mahallesi", fenerbahce.Properties.Name);

        Assert.DoesNotContain(
            payload.Features,
            feature => feature.Properties.Id.ToString() == fixture.KizilayNeighborhoodId);
    }

    [Fact]
    public async Task Neighborhoods_return_404_for_an_unknown_city()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/cities/99/neighborhoods");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("city_not_found", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Neighborhood_detail_returns_the_hierarchy_geometry_and_centroid()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/neighborhoods/{fixture.ModaNeighborhoodId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<NeighborhoodDetailResponse>();
        Assert.NotNull(payload);

        Assert.Equal("Moda Mahallesi", payload.Name);
        Assert.Equal("neighborhood", payload.Level);
        Assert.Equal("Kadıköy", payload.District.Name);
        Assert.Equal("İstanbul", payload.Province.Name);
        Assert.Equal("MultiPolygon", payload.Geometry.GetProperty("type").GetString());
        Assert.Equal("Point", payload.Centroid.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Neighborhood_detail_returns_404_for_an_unknown_id()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/neighborhoods/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("neighborhood_not_found", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Neighborhood_metrics_return_the_latest_value_with_source_and_date()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/neighborhoods/{fixture.ModaNeighborhoodId}/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<NeighborhoodMetricsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(long.Parse(fixture.ModaNeighborhoodId), payload.NeighborhoodId);

        var metric = Assert.Single(payload.Metrics);
        Assert.Equal("population_density", metric.Key);
        Assert.Equal(200, metric.Raw);
        Assert.Equal(0.5, metric.Normalized);
        Assert.Equal(QualityFlag.Estimated, metric.QualityFlag);
        Assert.Equal(new DateOnly(2025, 12, 31), metric.ObservedAt);
        Assert.Equal("2025", metric.ReferencePeriod);
        Assert.Equal("worldpop-test", metric.Source.Id);
        Assert.Equal("WorldPop test", metric.Source.Name);
        Assert.Equal("WorldPop (test)", metric.Source.Attribution);
    }

    [Fact]
    public async Task Neighborhood_metrics_return_404_for_an_unknown_id()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/neighborhoods/999999/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("neighborhood_not_found", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Neighborhoods_support_etag_revalidation()
    {
        using var client = fixture.Factory.CreateClient();
        using var first = await client.GetAsync("/api/v1/cities/34/neighborhoods");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cities/34/neighborhoods");
        request.Headers.IfNoneMatch.Add(etag);
        using var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(etag, second.Headers.ETag);
    }
}
