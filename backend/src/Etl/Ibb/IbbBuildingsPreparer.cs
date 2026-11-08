using System.Text;
using NeighbourhoodExplorer.Etl.Manifests;

namespace NeighbourhoodExplorer.Etl.Ibb;

public sealed record IbbBuildingRow(
    int Id,
    string DistrictName,
    string NeighborhoodName,
    long MahalleUavt,
    int BuildingsBefore1980,
    int BuildingsBetween1980And2000,
    int BuildingsAfter2000,
    int BuildingsWith1To4Floors,
    int BuildingsWith5To9Floors,
    int BuildingsWith9To19Floors,
    string DistrictNormalized,
    string NeighborhoodNormalized)
{
    public int BuildingsByAge => BuildingsBefore1980 + BuildingsBetween1980And2000 + BuildingsAfter2000;

    public int BuildingsByFloors => BuildingsWith1To4Floors + BuildingsWith5To9Floors + BuildingsWith9To19Floors;
}

public sealed record IbbCountCheck(string Name, long Expected, long Actual)
{
    public bool Matches => Expected == Actual;
}

public sealed record IbbPreparedBuildings(
    IReadOnlyList<IbbBuildingRow> Rows,
    IReadOnlyList<IbbCountCheck> Counts,
    IReadOnlyList<string> Failures,
    int RepairedNames);

public static class IbbBuildingsPreparer
{
    public static IbbPreparedBuildings Prepare(
        IbbDatastoreDocument document,
        ExpectedCounts expectedCounts)
    {
        var failures = new List<string>();
        var rows = new List<IbbBuildingRow>(document.Records.Count);
        var repairedNames = 0;

        if (document.Total != document.Records.Count)
        {
            failures.Add(
                $"le datastore annonce {document.Total} lignes mais en renvoie {document.Records.Count} " +
                "(pagination incomplète ?).");
        }

        foreach (var record in document.Records)
        {
            string district;
            string neighborhood;
            try
            {
                district = IbbTextRepair.Repair(record.DistrictName);
                neighborhood = IbbTextRepair.Repair(record.NeighborhoodName);
            }
            catch (Exception exception) when (exception is EncoderFallbackException or DecoderFallbackException)
            {
                failures.Add($"ligne {record.Id} : encodage illisible ({exception.Message}).");
                continue;
            }

            if (!string.Equals(district, record.DistrictName, StringComparison.Ordinal))
            {
                repairedNames++;
            }

            if (!string.Equals(neighborhood, record.NeighborhoodName, StringComparison.Ordinal))
            {
                repairedNames++;
            }

            var row = new IbbBuildingRow(
                record.Id,
                district,
                neighborhood,
                record.MahalleUavt,
                record.BuildingsBefore1980,
                record.BuildingsBetween1980And2000,
                record.BuildingsAfter2000,
                record.BuildingsWith1To4Floors,
                record.BuildingsWith5To9Floors,
                record.BuildingsWith9To19Floors,
                IbbNameNormalizer.NormalizeDistrict(district),
                IbbNameNormalizer.NormalizeNeighborhood(neighborhood));

            if (row.BuildingsByAge != row.BuildingsByFloors)
            {
                failures.Add(
                    $"ligne {record.Id} ({district} / {neighborhood}) : somme des tranches d'âge " +
                    $"{row.BuildingsByAge} différente de la somme des tranches d'étages {row.BuildingsByFloors}.");
            }

            rows.Add(row);
        }

        var counts = new List<IbbCountCheck>();
        AddCount(counts, failures, expectedCounts, ExpectedCountField.Rows, "rows", rows.Count);
        AddCount(counts, failures, expectedCounts, ExpectedCountField.Districts, "districts",
            rows.Select(row => row.DistrictNormalized).Distinct(StringComparer.Ordinal).Count());
        AddCount(counts, failures, expectedCounts, ExpectedCountField.Uavt, "uavt",
            rows.Select(row => row.MahalleUavt).Distinct().Count());
        AddCount(counts, failures, expectedCounts, ExpectedCountField.Buildings, "buildings",
            rows.Sum(row => (long)row.BuildingsByAge));

        return new IbbPreparedBuildings(rows, counts, failures, repairedNames);
    }

    private static void AddCount(
        ICollection<IbbCountCheck> counts,
        ICollection<string> failures,
        ExpectedCounts expectedCounts,
        ExpectedCountField field,
        string key,
        long actual)
    {
        if (!expectedCounts.TryGet(field, out var expected))
        {
            failures.Add($"le manifeste doit définir expected_counts.{key}.");
            return;
        }

        var check = new IbbCountCheck(key, expected, actual);
        counts.Add(check);
        if (!check.Matches)
        {
            failures.Add($"contrôle « {key} » : attendu {expected}, obtenu {actual}.");
        }
    }
}
