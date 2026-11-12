namespace NeighbourhoodExplorer.Etl.Reports;

public sealed record CoverageScope
{
    public required int NeighborhoodCount { get; init; }

    public required int DistrictCount { get; init; }

    public required int ExpectedNeighborhoods { get; init; }

    public required int ExpectedDistricts { get; init; }

    public void AppendFailures(List<string> failures)
    {
        if (NeighborhoodCount != ExpectedNeighborhoods)
        {
            failures.Add(
                $"couverture : {NeighborhoodCount} mahalles au lieu de {ExpectedNeighborhoods}.");
        }

        if (DistrictCount != ExpectedDistricts)
        {
            failures.Add($"couverture : {DistrictCount} ilçe au lieu de {ExpectedDistricts}.");
        }
    }
}
