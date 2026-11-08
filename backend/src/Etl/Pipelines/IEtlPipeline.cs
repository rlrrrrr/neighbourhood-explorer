using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed record EtlPipelineRequest(
    SourceManifest Manifest,
    string DataDirectory,
    string? ProvinceCode);

public interface IEtlPipeline
{
    PipelineId Id { get; }

    Task<EtlOutcome> RunAsync(EtlPipelineRequest request, CancellationToken cancellationToken);
}
