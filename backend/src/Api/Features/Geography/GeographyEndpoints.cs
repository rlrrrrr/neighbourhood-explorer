using System.Globalization;
using NeighbourhoodExplorer.Api.Http;

namespace NeighbourhoodExplorer.Api.Features.Geography;

public static class GeographyEndpoints
{
    private const int GeometryCacheSeconds = 86_400;

    public static IEndpointRouteBuilder MapGeographyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/cities", ReadCitiesAsync);
        endpoints.MapGet("/api/v1/cities/{cityId:int}/neighborhoods", ReadNeighborhoodsAsync);
        endpoints.MapGet("/api/v1/neighborhoods/{id:long}", ReadNeighborhoodAsync);
        return endpoints;
    }

    private static async Task<IResult> ReadCitiesAsync(
        DatabaseConnection database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (database.DataSource is not { } dataSource)
        {
            return ApiProblems.DatabaseUnavailable();
        }

        var cities = await CityReader.ReadAllAsync(dataSource, cancellationToken);
        return CachedJsonResponse.Create(
            context,
            new CitiesResponse(cities
                .Select(city => new CityResponse(city.PlateCode, city.Name, city.OfficialCode))
                .ToList()));
    }

    private static async Task<IResult> ReadNeighborhoodsAsync(
        int cityId,
        DatabaseConnection database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
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

        var features = await NeighborhoodReader.ReadFeaturesAsync(dataSource, city.Id, cancellationToken);
        return CachedJsonResponse.Create(
            context,
            new NeighborhoodFeatureCollection("FeatureCollection", features),
            GeometryCacheSeconds,
            "application/geo+json; charset=utf-8");
    }

    private static async Task<IResult> ReadNeighborhoodAsync(
        long id,
        DatabaseConnection database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (database.DataSource is not { } dataSource)
        {
            return ApiProblems.DatabaseUnavailable();
        }

        var neighborhood = await NeighborhoodReader.ReadDetailAsync(dataSource, id, cancellationToken);
        if (neighborhood is null)
        {
            return ApiProblems.NotFound(
                "neighborhood_not_found",
                $"Aucun quartier ne correspond à l'identifiant {id}.");
        }

        return CachedJsonResponse.Create(context, neighborhood, GeometryCacheSeconds);
    }
}
