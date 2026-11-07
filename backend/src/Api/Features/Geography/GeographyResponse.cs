using System.Text.Json;

namespace NeighbourhoodExplorer.Api.Features.Geography;

public sealed record CitiesResponse(IReadOnlyList<CityResponse> Cities);

public sealed record CityResponse(int Id, string Name, string OfficialCode);

public sealed record NeighborhoodFeatureCollection(string Type, IReadOnlyList<NeighborhoodFeature> Features);

public sealed record NeighborhoodFeature(
    string Type,
    JsonElement Geometry,
    NeighborhoodFeatureProperties Properties);

public sealed record NeighborhoodFeatureProperties(long Id, string Name, long DistrictId, string DistrictName);

public sealed record NeighborhoodDetailResponse(
    long Id,
    string Name,
    string Level,
    AdminAreaReference District,
    AdminAreaReference Province,
    JsonElement Geometry,
    JsonElement Centroid);

public sealed record AdminAreaReference(long Id, string Name);
