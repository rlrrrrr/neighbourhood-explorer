using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Pipelines;
using Npgsql;

namespace NeighbourhoodExplorer.Etl;

public static class EtlApplication
{
    public static Task<int> RunAsync(
        string[] arguments,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        RunAsync(arguments, output, error, TimeProvider.System, cancellationToken);

    public static async Task<int> RunAsync(
        string[] arguments,
        TextWriter output,
        TextWriter error,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        EtlInvocation invocation;
        try
        {
            invocation = EtlInvocationParser.Parse(arguments);
        }
        catch (EtlUsageException exception)
        {
            error.WriteLine($"ETL: {exception.Message}");
            return 2;
        }

        var settings = EtlSettings.FromEnvironment();
        if (settings.ConnectionString is null)
        {
            error.WriteLine("ETL: DATABASE_CONNECTION_STRING manquant.");
            return 2;
        }

        var manifestPath = Path.Combine(settings.DataDirectory, "sources", $"{invocation.SourceId}.yaml");
        if (!File.Exists(manifestPath))
        {
            error.WriteLine($"ETL: manifeste introuvable : {manifestPath}");
            return 2;
        }

        SourceManifest manifest;
        try
        {
            manifest = SourceManifestReader.Read(manifestPath);
        }
        catch (ManifestException exception)
        {
            error.WriteLine($"ETL: manifeste invalide : {exception.Message}");
            return 2;
        }

        if (!string.Equals(manifest.Id, invocation.SourceId, StringComparison.Ordinal))
        {
            error.WriteLine(
                $"ETL: l'identifiant du manifeste « {manifest.Id} » ne correspond pas à la source demandée " +
                $"« {invocation.SourceId} ».");
            return 2;
        }

        var missingFields = SourceManifestReader.FindMissingFields(manifest);
        if (missingFields.Count > 0)
        {
            error.WriteLine(
                $"ETL: manifeste {manifestPath} incomplet : {string.Join(", ", missingFields)}. " +
                "Un humain doit avoir lu la licence et renseigné verified_by/verified_at ; ingestion refusée.");
            return 3;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NeighbourhoodExplorerEtl/1.0");

        await using var dataSource = CreateDataSource(settings.ConnectionString);
        var runRecorder = new EtlRunRecorder(dataSource, manifest.Id, timeProvider);
        long? runId = null;

        try
        {
            await EtlSchema.EnsureAsync(dataSource, cancellationToken);
            runId = await runRecorder.StartAsync(cancellationToken);

            var console = new TextWriterEtlConsole(output);
            var pipelines = EtlCompositionRoot.CreatePipelines(
                new EtlCompositionRootOptions(
                    dataSource,
                    console,
                    timeProvider,
                    settings.ConnectionString,
                    httpClient,
                    settings.TuikApiKey));

            if (!pipelines.TryGetValue(manifest.Pipeline, out var pipeline))
            {
                throw new EtlFailureException($"pipeline non enregistré : {manifest.Pipeline.ToWireValue()}");
            }

            var outcome = await pipeline.RunAsync(
                new EtlPipelineRequest(
                    manifest,
                    settings.DataDirectory,
                    invocation.ProvinceCode),
                cancellationToken);

            await runRecorder.FinishAsync(
                runId.Value,
                new EtlRunResult(
                    outcome.Success,
                    outcome.InputSha256,
                    outcome.RowCounts,
                    outcome.ErrorMessage),
                cancellationToken);

            if (!outcome.Success)
            {
                error.WriteLine($"ETL: échec : {outcome.ErrorMessage}");
                return 1;
            }

            output.WriteLine("ETL: terminé.");
            return 0;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (runId is long failedRunId)
            {
                try
                {
                    await runRecorder.FinishAsync(
                        failedRunId,
                        new EtlRunResult(
                            false,
                            null,
                            new Dictionary<string, int>(StringComparer.Ordinal),
                            exception.Message),
                        CancellationToken.None);
                }
                catch (Exception recorderException)
                {
                    error.WriteLine($"ETL: impossible d'enregistrer l'échec : {recorderException.Message}");
                }
            }

            error.WriteLine($"ETL: échec : {exception.Message}");
            return 1;
        }
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.CommandTimeout = Math.Max(
            builder.ConnectionStringBuilder.CommandTimeout,
            OfflineCommandTimeoutSeconds);
        return builder.Build();
    }

    private const int OfflineCommandTimeoutSeconds = 900;
}
