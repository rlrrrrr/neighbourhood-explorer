using System.Globalization;
using NeighbourhoodExplorer.Etl.Osm;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopRasterLoadResult(
    int TileCount,
    double TotalPopulation,
    long ValidCellCount,
    string PostgisVersion);

public sealed record WorldPopRasterLoadRequest(
    NpgsqlDataSource DataSource,
    OsmDatabaseTarget Target,
    string ProjectedPath,
    TextWriter Output);

public static class WorldPopRasterLoader
{
    public static async Task<WorldPopRasterLoadResult> LoadAsync(
        WorldPopRasterLoadRequest request,
        CancellationToken cancellationToken)
    {
        var sqlPath = Path.Combine(
            Path.GetTempPath(),
            $"worldpop-{Guid.NewGuid():N}.sql");

        try
        {
            request.Output.WriteLine(
                $"ETL: raster2pgsql, import de {Path.GetFileName(request.ProjectedPath)}");

            var raster = await WorldPopCommandRunner.RunToFileAsync(
                new WorldPopCommand(
                    "raster2pgsql",
                    [
                        "-s",
                        "32635",
                        "-I",
                        "-C",
                        "-M",
                        "-t",
                        "100x100",
                        "-d",
                        request.ProjectedPath,
                        "public.worldpop_raster",
                    ]),
                sqlPath,
                cancellationToken);

            if (raster.ExitCode != 0)
            {
                throw new EtlFailureException(
                    $"raster2pgsql a échoué (code {raster.ExitCode}) : " +
                    $"{WorldPopCommandRunner.Tail(raster.StandardError)}");
            }

            var psql = await WorldPopCommandRunner.RunAsync(
                new WorldPopCommand(
                    "psql",
                    [
                        "-h",
                        request.Target.Host,
                        "-p",
                        request.Target.Port.ToString(CultureInfo.InvariantCulture),
                        "-U",
                        request.Target.Username,
                        "-d",
                        request.Target.Database,
                        "-v",
                        "ON_ERROR_STOP=1",
                        "-q",
                        "-f",
                        sqlPath,
                    ],
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["PGPASSWORD"] = request.Target.Password,
                    }),
                cancellationToken);

            if (psql.ExitCode != 0)
            {
                throw new EtlFailureException(
                    $"psql a échoué (code {psql.ExitCode}) : " +
                    $"{WorldPopCommandRunner.Tail(psql.StandardError)}");
            }

            await using var connection = await request.DataSource.OpenConnectionAsync(cancellationToken);
            await using var stats = connection.CreateCommand();
            stats.CommandText = """
                SELECT count(*)::int,
                       coalesce(sum((ST_SummaryStats(rast, 1, true)).sum), 0),
                       coalesce(sum((ST_SummaryStats(rast, 1, true)).count), 0)
                FROM worldpop_raster
                """;

            await using var reader = await stats.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var tileCount = reader.GetInt32(0);
            var totalPopulation = reader.GetDouble(1);
            var validCellCount = reader.GetInt64(2);
            await reader.CloseAsync();

            await using var version = connection.CreateCommand();
            version.CommandText = "SELECT postgis_full_version()";
            var postgisVersion = (string)(await version.ExecuteScalarAsync(cancellationToken))!;

            return new WorldPopRasterLoadResult(
                tileCount,
                totalPopulation,
                validCellCount,
                postgisVersion);
        }
        finally
        {
            if (File.Exists(sqlPath))
            {
                File.Delete(sqlPath);
            }
        }
    }
}
