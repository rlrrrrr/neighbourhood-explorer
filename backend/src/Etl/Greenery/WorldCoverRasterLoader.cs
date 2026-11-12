using System.Globalization;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Osm;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed record WorldCoverRasterLoadResult(int TileCount, string PostgisVersion);

public sealed record WorldCoverRasterLoadRequest(
    OsmDatabaseTarget Target,
    string CroppedPath);

public sealed class WorldCoverRasterLoader(
    NpgsqlDataSource dataSource,
    IExternalToolRunner toolRunner,
    IEtlConsole console)
{
    private const string RasterTable = "public.worldcover_raster";

    public async Task<WorldCoverRasterLoadResult> LoadAsync(
        WorldCoverRasterLoadRequest request,
        CancellationToken cancellationToken)
    {
        var sqlPath = Path.Combine(
            Path.GetTempPath(),
            $"worldcover-{Guid.NewGuid():N}.sql");

        try
        {
            console.WriteLine(
                $"ETL: raster2pgsql, import de {Path.GetFileName(request.CroppedPath)}");

            var raster = await toolRunner.RunToFileAsync(
                new ExternalToolCommand(
                    "raster2pgsql",
                    [
                        "-s",
                        "4326",
                        "-I",
                        "-C",
                        "-M",
                        "-t",
                        "100x100",
                        "-d",
                        request.CroppedPath,
                        RasterTable,
                    ]),
                sqlPath,
                cancellationToken);

            if (raster.ExitCode != 0)
            {
                throw new EtlFailureException(
                    $"raster2pgsql a échoué (code {raster.ExitCode}) : {raster.Tail()}");
            }

            var psql = await toolRunner.RunAsync(
                new ExternalToolCommand(
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
                    $"psql a échoué (code {psql.ExitCode}) : {psql.Tail()}");
            }

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var tileCount = connection.CreateCommand();
            tileCount.CommandText = $"SELECT count(*)::int FROM {RasterTable}";
            var tileCountValue = (int)(await tileCount.ExecuteScalarAsync(cancellationToken))!;

            await using var version = connection.CreateCommand();
            version.CommandText = "SELECT postgis_full_version()";
            var postgisVersion = (string)(await version.ExecuteScalarAsync(cancellationToken))!;

            return new WorldCoverRasterLoadResult(tileCountValue, postgisVersion);
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
