using System.Globalization;
using Npgsql;

namespace NeighbourhoodExplorer.Api.Features.Geography;

public sealed record CityReference(long Id, string Name);

public sealed record CitySummary(int PlateCode, string Name, string OfficialCode, long AdminAreaId);

public static class CityReader
{
    public static async Task<CityReference?> FindByOfficialCodeAsync(
        NpgsqlDataSource dataSource,
        string officialCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name
            FROM admin_area
            WHERE level = 'province' AND official_code = @official_code
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("official_code", officialCode);
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
            if (officialCode.Length <= 3
                || !int.TryParse(
                    officialCode[3..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var plateCode))
            {
                continue;
            }

            cities.Add(new CitySummary(plateCode, reader.GetString(1), officialCode, reader.GetInt64(0)));
        }

        return cities;
    }
}
