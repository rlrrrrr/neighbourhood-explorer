using NeighbourhoodExplorer.Etl.Ibb;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Osm;
using NeighbourhoodExplorer.Etl.Pipelines;
using Npgsql;

namespace NeighbourhoodExplorer.Etl;

public sealed record EtlCompositionRootOptions(
    NpgsqlDataSource DataSource,
    IEtlConsole Console,
    TimeProvider TimeProvider,
    string ConnectionString,
    HttpClient HttpClient,
    string? TuikApiKey);

public static class EtlCompositionRoot
{
    public static IReadOnlyDictionary<PipelineId, IEtlPipeline> CreatePipelines(
        EtlCompositionRootOptions options)
    {
        var target = OsmDatabaseTarget.FromConnectionString(options.ConnectionString);
        var toolRunner = new ProcessExternalToolRunner();
        var aliasTable = IbbNeighborhoodAliasTable.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Ibb",
            "Aliases",
            "ibb-neighborhood-aliases.json"));

        return new Dictionary<PipelineId, IEtlPipeline>
        {
            [PipelineId.OsmAdminBoundaries] = new OsmAdministrativeAreasPipeline(
                options.DataSource,
                target,
                toolRunner,
                options.Console,
                options.TimeProvider),
            [PipelineId.IbbBuildings2017] = new IbbBuildingsPipeline(
                options.DataSource,
                options.Console,
                options.TimeProvider,
                new IbbNeighborhoodMatcher(aliasTable)),
            [PipelineId.TuikAdnks] = new TuikAdnksPipeline(
                options.DataSource,
                options.Console,
                options.TimeProvider,
                options.HttpClient,
                options.TuikApiKey),
            [PipelineId.WorldPopIstanbul2025] = new WorldPopPipeline(
                options.DataSource,
                target,
                toolRunner,
                options.Console,
                options.TimeProvider),
        };
    }
}
