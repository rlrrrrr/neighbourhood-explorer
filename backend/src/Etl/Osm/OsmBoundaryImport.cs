using System.Diagnostics;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record OsmImportRequest(NpgsqlDataSource DataSource, string RawFilePath);

public sealed class OsmBoundaryImport(OsmDatabaseTarget target, TextWriter output)
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

        var startInfo = new ProcessStartInfo("osm2pgsql")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--create");
        startInfo.ArgumentList.Add("--output=flex");
        startInfo.ArgumentList.Add($"--style={stylePath}");
        startInfo.ArgumentList.Add("--slim");
        startInfo.ArgumentList.Add("--drop");
        startInfo.ArgumentList.Add("--cache=1024");
        startInfo.ArgumentList.Add("--number-processes=2");
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(target.Database);
        startInfo.ArgumentList.Add("-H");
        startInfo.ArgumentList.Add(target.Host);
        startInfo.ArgumentList.Add("-P");
        startInfo.ArgumentList.Add(target.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-U");
        startInfo.ArgumentList.Add(target.Username);
        startInfo.ArgumentList.Add(request.RawFilePath);
        startInfo.Environment["PGPASSWORD"] = target.Password;

        output.WriteLine($"ETL: osm2pgsql {version}, import de {Path.GetFileName(request.RawFilePath)}");

        using var process = Process.Start(startInfo)
            ?? throw new EtlFailureException("impossible de démarrer osm2pgsql.");

        var tail = new Queue<string>();
        var errorPump = PumpAsync(process.StandardError, line =>
        {
            lock (tail)
            {
                tail.Enqueue(line);
                while (tail.Count > 40)
                {
                    tail.Dequeue();
                }
            }
        }, cancellationToken);
        var outputPump = PumpAsync(process.StandardOutput, _ => { }, cancellationToken);

        try
        {
            await Task.WhenAll(errorPump, outputPump, process.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
        {
            string details;
            lock (tail)
            {
                details = string.Join(" | ", tail);
            }

            throw new EtlFailureException($"osm2pgsql a échoué (code {process.ExitCode}) : {details}");
        }

        return version;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            onLine(line);
        }
    }

    private static async Task<string> ReadVersionAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("osm2pgsql")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--version");

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new EtlFailureException("impossible de démarrer osm2pgsql.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception)
        {
            throw new EtlFailureException(
                "osm2pgsql est absent du système : lancer l'ETL dans l'image Docker (mise etl).");
        }

        using (process)
        {
            var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var firstLine = (standardOutput + standardError)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return firstLine ?? "version inconnue";
        }
    }

    private static async Task DropStagingTablesAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "DROP TABLE IF EXISTS osm_admin_boundaries CASCADE; DROP TABLE IF EXISTS osm2pgsql_properties CASCADE;");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
