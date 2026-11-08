using System.Globalization;
using NeighbourhoodExplorer.Etl.Infrastructure;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record OsmImportRequest(NpgsqlDataSource DataSource, string RawFilePath);

public sealed class OsmBoundaryImport(
    OsmDatabaseTarget target,
    IExternalToolRunner toolRunner,
    IEtlConsole console)
{
    public async Task<string> RunAsync(OsmImportRequest request, CancellationToken cancellationToken)
    {
        var version = await ReadVersionAsync(cancellationToken);
        await DropStagingTablesAsync(request.DataSource, cancellationToken);

        var stylePath = Path.Combine(AppContext.BaseDirectory, "Styles", "osm-admin-boundaries.lua");
        if (!File.Exists(stylePath))
        {
            throw new EtlFailureException($"style osm2pgsql introuvable : {stylePath}");
        }

        console.WriteLine($"ETL: osm2pgsql {version}, import de {Path.GetFileName(request.RawFilePath)}");

        var result = await toolRunner.RunAsync(
            new ExternalToolCommand(
                "osm2pgsql",
                [
                    "--create",
                    "--output=flex",
                    $"--style={stylePath}",
                    "--slim",
                    "--drop",
                    "--cache=1024",
                    "--number-processes=2",
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
                }),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"osm2pgsql a échoué (code {result.ExitCode}) : {result.Tail()}");
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
            "DROP TABLE IF EXISTS osm_admin_boundaries CASCADE; DROP TABLE IF EXISTS osm2pgsql_properties CASCADE;");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
