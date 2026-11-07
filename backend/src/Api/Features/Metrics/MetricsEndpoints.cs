using System.Globalization;
using NeighbourhoodExplorer.Api.Features.Geography;
using NeighbourhoodExplorer.Api.Http;

namespace NeighbourhoodExplorer.Api.Features.Metrics;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/metrics", ReadMetricsAsync);
        endpoints.MapGet("/api/v1/cities/{cityId:int}/metric-values", ReadMetricValuesAsync);
        endpoints.MapGet("/api/v1/neighborhoods/{id:long}/metrics", ReadNeighborhoodMetricsAsync);
        return endpoints;
    }

    private static async Task<IResult> ReadMetricsAsync(
        DatabaseConnection database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (database.DataSource is not { } dataSource)
        {
            return ApiProblems.DatabaseUnavailable();
        }

        var metrics = await MetricsReader.ReadDefinitionsAsync(dataSource, cancellationToken);
        return CachedJsonResponse.Create(context, new MetricsResponse(metrics));
    }

    private static async Task<IResult> ReadMetricValuesAsync(
        int cityId,
        string? metrics,
        DatabaseConnection database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var metricKeys = ParseMetricKeys(metrics);
        if (metricKeys.Count == 0)
        {
            return ApiProblems.BadRequest(
                "metrics_required",
                "Le paramètre metrics est requis : clés de métriques séparées par des virgules.");
        }

        if (database.DataSource is not { } dataSource)
        {
            return ApiProblems.DatabaseUnavailable();
        }

        var officialCode = $"TR-{cityId.ToString("D2", CultureInfo.InvariantCulture)}";
        var city = await CityReader.FindByOfficialCodeAsync(dataSource, officialCode, cancellationToken);
        if (city is null)
        {
            return ApiProblems.NotFound(
                "city_not_found",
                $"Aucune province ne correspond à official_code {officialCode}.");
        }

        var knownKeys = await MetricsReader.ReadKnownKeysAsync(dataSource, metricKeys, cancellationToken);
        var unknownKeys = metricKeys
            .Where(key => !knownKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        if (unknownKeys.Count > 0)
        {
            return ApiProblems.BadRequest(
                "unknown_metric",
                $"Métrique(s) inconnue(s) : {string.Join(", ", unknownKeys)}.");
        }

        var values = await MetricsReader.ReadLatestValuesAsync(
            dataSource,
            city.Id,
            metricKeys,
            cancellationToken);
        return CachedJsonResponse.Create(context, values);
    }

    private static async Task<IResult> ReadNeighborhoodMetricsAsync(
        long id,
        DatabaseConnection database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (database.DataSource is not { } dataSource)
        {
            return ApiProblems.DatabaseUnavailable();
        }

        var metrics = await MetricsReader.ReadNeighborhoodMetricsAsync(
            dataSource,
            id,
            cancellationToken);
        if (metrics is null)
        {
            return ApiProblems.NotFound(
                "neighborhood_not_found",
                $"Aucun quartier ne correspond à l'identifiant {id}.");
        }

        return CachedJsonResponse.Create(context, metrics);
    }

    private static IReadOnlyList<string> ParseMetricKeys(string? rawMetricKeys)
    {
        if (string.IsNullOrWhiteSpace(rawMetricKeys))
        {
            return [];
        }

        return rawMetricKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
