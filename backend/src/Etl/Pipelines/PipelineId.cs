namespace NeighbourhoodExplorer.Etl.Pipelines;

public enum PipelineId
{
    OsmAdminBoundaries,
    IbbBuildings2017,
    TuikAdnks,
    WorldPopIstanbul2025,
}

public static class PipelineIdExtensions
{
    public static string ToWireValue(this PipelineId pipelineId) => pipelineId switch
    {
        PipelineId.OsmAdminBoundaries => "osm-admin-boundaries",
        PipelineId.IbbBuildings2017 => "ibb-buildings-2017",
        PipelineId.TuikAdnks => "tuik-adnks",
        PipelineId.WorldPopIstanbul2025 => "worldpop-istanbul-2025",
        _ => throw new ArgumentOutOfRangeException(nameof(pipelineId), pipelineId, null),
    };

    public static bool TryParse(string value, out PipelineId pipelineId)
    {
        foreach (var candidate in Enum.GetValues<PipelineId>())
        {
            if (string.Equals(candidate.ToWireValue(), value, StringComparison.Ordinal))
            {
                pipelineId = candidate;
                return true;
            }
        }

        pipelineId = default;
        return false;
    }
}
