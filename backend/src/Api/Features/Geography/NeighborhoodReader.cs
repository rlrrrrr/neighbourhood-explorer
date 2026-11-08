using System.Text.Json;
using Npgsql;

namespace NeighbourhoodExplorer.Api.Features.Geography;

public static class NeighborhoodReader
{
    public static async Task<IReadOnlyList<NeighborhoodFeature>> ReadFeaturesAsync(
        NpgsqlDataSource dataSource,
        long cityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                n.id,
                n.name,
                d.id,
                d.name,
                ST_AsGeoJSON(COALESCE(n.display_geometry, n.geometry), 5)
            FROM admin_area n
            JOIN admin_area d ON d.id = n.parent_id
            WHERE n.level = 'neighborhood'
              AND d.level = 'district'
              AND d.parent_id = @city_id
            ORDER BY n.id
            """;

        var features = new List<NeighborhoodFeature>();
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("city_id", cityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            using var geometryDocument = JsonDocument.Parse(reader.GetString(4));
            features.Add(new NeighborhoodFeature(
                Type: "Feature",
                Geometry: geometryDocument.RootElement.Clone(),
                Properties: new NeighborhoodFeatureProperties(
                    Id: reader.GetInt64(0),
                    Name: reader.GetString(1),
                    DistrictId: reader.GetInt64(2),
                    DistrictName: reader.GetString(3))));
        }

        return features;
    }

    public static async Task<NeighborhoodDetailResponse?> ReadDetailAsync(
        NpgsqlDataSource dataSource,
        long neighborhoodId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                n.id,
                n.name,
                n.level,
                d.id,
                d.name,
                p.id,
                p.name,
                ST_AsGeoJSON(n.geometry),
                ST_AsGeoJSON(n.centroid)
            FROM admin_area n
            JOIN admin_area d ON d.id = n.parent_id
            JOIN admin_area p ON p.id = d.parent_id
            WHERE n.id = @id AND n.level = 'neighborhood'
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", neighborhoodId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        using var geometryDocument = JsonDocument.Parse(reader.GetString(7));
        using var centroidDocument = JsonDocument.Parse(reader.GetString(8));
        return new NeighborhoodDetailResponse(
            Id: reader.GetInt64(0),
            Name: reader.GetString(1),
            Level: reader.GetString(2),
            District: new AdminAreaReference(reader.GetInt64(3), reader.GetString(4)),
            Province: new AdminAreaReference(reader.GetInt64(5), reader.GetString(6)),
            Geometry: geometryDocument.RootElement.Clone(),
            Centroid: centroidDocument.RootElement.Clone());
    }
}
