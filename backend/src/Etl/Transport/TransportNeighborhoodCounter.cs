using Npgsql;

namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportNeighborhoodCount(
    long AdminAreaId,
    string DistrictName,
    string NeighborhoodName,
    int BusStops,
    int RailStations,
    int FerryPiers,
    double AreaKm2);

public sealed record TransportUnassignedPoint(
    TransportMode Mode,
    string Name,
    double Longitude,
    double Latitude);

public sealed record TransportAssignmentStats(
    int Polygon,
    int Nearest,
    int Unassigned,
    IReadOnlyList<TransportUnassignedPoint> UnassignedPoints);

public sealed record TransportCountResult(
    IReadOnlyList<TransportNeighborhoodCount> Neighborhoods,
    TransportAssignmentStats Assignments);

public static class TransportNeighborhoodCounter
{
    private const double NearestMaximumMetres = 500;

    public static async Task<TransportCountResult> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<TransportPoint> points,
        CancellationToken cancellationToken)
    {
        await CreateTemporaryTableAsync(connection, transaction, cancellationToken);
        await InsertPointsAsync(connection, transaction, points, cancellationToken);
        await AssignCoveredAsync(connection, transaction, cancellationToken);
        await AssignNearestAsync(connection, transaction, cancellationToken);
        var assignments = await ReadAssignmentsAsync(connection, transaction, cancellationToken);
        var neighborhoods = await ReadNeighborhoodsAsync(connection, transaction, cancellationToken);
        return new TransportCountResult(neighborhoods, assignments);
    }

    private static async Task CreateTemporaryTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TEMP TABLE transport_point_temp (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                mode text NOT NULL,
                name text NOT NULL,
                geom geometry(Point, 4326) NOT NULL,
                admin_area_id bigint,
                assignment text
            ) ON COMMIT DROP
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPointsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<TransportPoint> points,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO transport_point_temp (mode, name, geom)
            SELECT mode, name, ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)
            FROM unnest(@modes, @names, @longitudes, @latitudes)
                AS source(mode, name, longitude, latitude)
            """;
        command.Parameters.AddWithValue(
            "modes",
            points.Select(point => point.Mode.ToWireValue()).ToArray());
        command.Parameters.AddWithValue(
            "names",
            points.Select(point => point.Name).ToArray());
        command.Parameters.AddWithValue(
            "longitudes",
            points.Select(point => point.Longitude).ToArray());
        command.Parameters.AddWithValue(
            "latitudes",
            points.Select(point => point.Latitude).ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AssignCoveredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE transport_point_temp point
            SET admin_area_id = covered.admin_area_id,
                assignment = 'polygon'
            FROM (
                SELECT source.id, area.id AS admin_area_id
                FROM transport_point_temp source
                JOIN LATERAL (
                    SELECT candidate.id
                    FROM admin_area candidate
                    WHERE candidate.level = 'neighborhood'
                      AND ST_Covers(candidate.geometry, source.geom)
                    ORDER BY candidate.id
                    LIMIT 1
                ) area ON TRUE
            ) covered
            WHERE point.id = covered.id
              AND point.admin_area_id IS NULL
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AssignNearestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE transport_point_temp point
            SET admin_area_id = nearest.admin_area_id,
                assignment = 'nearest'
            FROM (
                SELECT source.id, area.id AS admin_area_id
                FROM transport_point_temp source
                JOIN LATERAL (
                    SELECT candidate.id
                    FROM admin_area candidate
                    WHERE candidate.level = 'neighborhood'
                      AND ST_DWithin(candidate.geometry, source.geom, 0.01)
                      AND ST_DWithin(
                          candidate.geometry::geography,
                          source.geom::geography,
                          @maximum_metres)
                    ORDER BY ST_Distance(
                        candidate.geometry::geography,
                        source.geom::geography),
                        candidate.id
                    LIMIT 1
                ) area ON TRUE
                WHERE source.admin_area_id IS NULL
            ) nearest
            WHERE point.id = nearest.id
            """;
        command.Parameters.AddWithValue("maximum_metres", NearestMaximumMetres);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<TransportAssignmentStats> ReadAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                count(*) FILTER (WHERE assignment = 'polygon')::int,
                count(*) FILTER (WHERE assignment = 'nearest')::int,
                count(*) FILTER (WHERE admin_area_id IS NULL)::int
            FROM transport_point_temp
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var polygon = reader.GetInt32(0);
        var nearest = reader.GetInt32(1);
        var unassigned = reader.GetInt32(2);
        await reader.DisposeAsync();

        var unassignedPoints = new List<TransportUnassignedPoint>();
        if (unassigned > 0)
        {
            await using var unassignedCommand = connection.CreateCommand();
            unassignedCommand.Transaction = transaction;
            unassignedCommand.CommandText = """
                SELECT mode, name, ST_X(geom), ST_Y(geom)
                FROM transport_point_temp
                WHERE admin_area_id IS NULL
                ORDER BY mode, name
                """;
            await using var unassignedReader = await unassignedCommand.ExecuteReaderAsync(cancellationToken);
            while (await unassignedReader.ReadAsync(cancellationToken))
            {
                unassignedPoints.Add(new TransportUnassignedPoint(
                    ParseMode(unassignedReader.GetString(0)),
                    unassignedReader.GetString(1),
                    unassignedReader.GetDouble(2),
                    unassignedReader.GetDouble(3)));
            }
        }

        return new TransportAssignmentStats(polygon, nearest, unassigned, unassignedPoints);
    }

    private static async Task<IReadOnlyList<TransportNeighborhoodCount>> ReadNeighborhoodsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 1800;
        command.CommandText = """
            SELECT neighborhood.id,
                   district.name,
                   neighborhood.name,
                   count(point.id) FILTER (WHERE point.mode = 'bus')::int,
                   count(point.id) FILTER (WHERE point.mode = 'rail')::int,
                   count(point.id) FILTER (WHERE point.mode = 'ferry')::int,
                   ST_Area(ST_Transform(neighborhood.geometry, 32635)) / 1000000.0
            FROM admin_area neighborhood
            JOIN admin_area district ON district.id = neighborhood.parent_id
            LEFT JOIN transport_point_temp point ON point.admin_area_id = neighborhood.id
            WHERE neighborhood.level = 'neighborhood'
            GROUP BY neighborhood.id, district.name, neighborhood.name, neighborhood.geometry
            ORDER BY district.name, neighborhood.name
            """;

        var neighborhoods = new List<TransportNeighborhoodCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            neighborhoods.Add(new TransportNeighborhoodCount(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetDouble(6)));
        }

        return neighborhoods;
    }

    private static TransportMode ParseMode(string value) => value switch
    {
        "bus" => TransportMode.Bus,
        "rail" => TransportMode.Rail,
        "ferry" => TransportMode.Ferry,
        _ => throw new EtlFailureException($"mode de transport inconnu en base : {value}"),
    };
}
