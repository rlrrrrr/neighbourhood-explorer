using System.Globalization;
using NeighbourhoodExplorer.Etl.Infrastructure;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed class OsmUrbanFeaturesImport(
    OsmDatabaseTarget target,
    IExternalToolRunner toolRunner,
    IEtlConsole console)
{
    private const int CacheMegabytes = 2048;
    private const int ProcessCount = 4;

    public async Task<string> RunAsync(
        UrbanFeatureImportRequest request,
        CancellationToken cancellationToken)
    {
        var version = await ReadVersionAsync(cancellationToken);
        await DropStagingTablesAsync(request.DataSource, cancellationToken);

        var stylePath = Path.Combine(AppContext.BaseDirectory, "Styles", "osm-urban-features.lua");
        if (!File.Exists(stylePath))
        {
            throw new EtlFailureException($"style osm2pgsql introuvable : {stylePath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.LogFilePath)!);

        console.WriteLine(
            $"ETL: osm2pgsql {version}, import des objets urbains de " +
            $"{Path.GetFileName(request.RawFilePath)} " +
            $"(emprise {request.Bounds.ToOsm2pgsqlArgument()}, journal {request.LogFilePath})");

        var result = await toolRunner.RunWithDiagnosticsLogAsync(
            new ExternalToolCommand(
                "osm2pgsql",
                [
                    "--create",
                    "--output=flex",
                    $"--style={stylePath}",
                    "--slim",
                    "--drop",
                    $"--cache={CacheMegabytes.ToString(CultureInfo.InvariantCulture)}",
                    $"--number-processes={ProcessCount.ToString(CultureInfo.InvariantCulture)}",
                    $"--bbox={request.Bounds.ToOsm2pgsqlArgument()}",
                    "-d",
                    target.Database,
                    "-H",
                    target.Host,
                    "-P",
                    target.Port.ToString(CultureInfo.InvariantCulture),
                    "-U",
                    target.Username,
                    request.RawFilePath,
                ],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["PGPASSWORD"] = target.Password,
                    ["PGOPTIONS"] = "-c synchronous_commit=off -c maintenance_work_mem=512MB -c work_mem=64MB",
                }),
            request.LogFilePath,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"osm2pgsql a échoué (code {result.ExitCode}) ; journal " +
                $"{request.LogFilePath} : {result.Tail()}");
        }

        return version;
    }

    private async Task<string> ReadVersionAsync(CancellationToken cancellationToken)
    {
        var result = await toolRunner.RunAsync(
            new ExternalToolCommand("osm2pgsql", ["--version"]),
            cancellationToken);

        var firstLine = (result.StandardOutput + result.StandardError)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return firstLine ?? "version inconnue";
    }

    private static async Task DropStagingTablesAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            DROP TABLE IF EXISTS osm_urban_poi, osm_urban_area, osm_urban_highway,
                osm2pgsql_properties CASCADE;
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
