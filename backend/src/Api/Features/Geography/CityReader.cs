using Npgsql;

namespace NeighbourhoodExplorer.Api.Features.Geography;

public sealed record CityReference(long Id, string Name);

public sealed record CitySummary(PlateCode PlateCode, string Name, string OfficialCode, long AdminAreaId);

public static class CityReader
{
    public static async Task<CityReference?> FindByPlateCodeAsync(
        NpgsqlDataSource dataSource,
        PlateCode plateCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name
            FROM admin_area
            WHERE level = 'province' AND official_code = @official_code
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("official_code", plateCode.ToOfficialCode());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CityReference(reader.GetInt64(0), reader.GetString(1))
            : null;
    }

    public static async Task<IReadOnlyList<CitySummary>> ReadAllAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, official_code
            FROM admin_area
            WHERE level = 'province' AND official_code LIKE 'TR-%'
            ORDER BY official_code
            """;

        var cities = new List<CitySummary>();
        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var officialCode = reader.GetString(2);
            if (!PlateCode.TryParseOfficialCode(officialCode, out var plateCode))
            {
                throw new InvalidOperationException(
                    $"official_code de province invalide : « {officialCode} ».");
            }

            cities.Add(new CitySummary(
                plateCode,
                reader.GetString(1),
                officialCode,
                reader.GetInt64(0)));
        }

        return cities;
    }
}
