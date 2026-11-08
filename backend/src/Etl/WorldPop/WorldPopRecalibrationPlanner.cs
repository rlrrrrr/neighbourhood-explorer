namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopTuikDistrict(
    string Code,
    string Name,
    string NameNormalized,
    int Population);

public sealed record WorldPopTuikProvince(string Code, string Name, int Population);

public sealed record WorldPopOsmDistrict(long Id, string Name, string NameNormalized);

public sealed record WorldPopDistrictSum(
    double Sum,
    int NeighborhoodCount,
    int ZeroNeighborhoodCount);

public sealed record WorldPopPlannerInput(
    IReadOnlyList<WorldPopTuikDistrict> TuikDistricts,
    WorldPopTuikProvince? TuikProvince,
    IReadOnlyList<WorldPopOsmDistrict> OsmDistricts,
    IReadOnlyDictionary<long, WorldPopDistrictSum> WorldpopSums,
    int ExpectedDistricts,
    int ExpectedProvincePopulation);

public sealed record WorldPopPlan(
    IReadOnlyList<WorldPopCoefficient> Coefficients,
    IReadOnlyList<string> UnmatchedDistricts,
    IReadOnlyList<string> Failures)
{
    public int TotalAdnks => Coefficients.Sum(coefficient => coefficient.AdnksTotal);
}

public static class WorldPopRecalibrationPlanner
{
    private const double MinimumCoefficient = 0.7;
    private const double MaximumCoefficient = 1.3;

    public static WorldPopPlan Build(WorldPopPlannerInput input)
    {
        var failures = new List<string>();
        var unmatched = new List<string>();

        if (input.TuikDistricts.Count != input.ExpectedDistricts)
        {
            failures.Add(
                $"TÜİK : {input.TuikDistricts.Count} ilçe au lieu de {input.ExpectedDistricts}.");
        }

        if (input.OsmDistricts.Count != input.ExpectedDistricts)
        {
            failures.Add(
                $"OSM : {input.OsmDistricts.Count} ilçe au lieu de {input.ExpectedDistricts}.");
        }

        if (input.TuikProvince is null)
        {
            failures.Add("TÜİK : total provincial absent.");
        }
        else
        {
            var districtSum = input.TuikDistricts.Sum(district => (long)district.Population);
            if (districtSum != input.TuikProvince.Population)
            {
                failures.Add(
                    $"TÜİK : somme des ilçe {districtSum} ≠ total provincial " +
                    $"{input.TuikProvince.Population}.");
            }

            if (input.TuikProvince.Population != input.ExpectedProvincePopulation)
            {
                failures.Add(
                    $"TÜİK : population provinciale {input.TuikProvince.Population} ≠ attendu " +
                    $"{input.ExpectedProvincePopulation}.");
            }
        }

        var osmByName = new Dictionary<string, WorldPopOsmDistrict>(StringComparer.Ordinal);
        foreach (var district in input.OsmDistricts)
        {
            if (!osmByName.TryAdd(district.NameNormalized, district))
            {
                failures.Add($"OSM : nom d'ilçe normalisé en double « {district.NameNormalized} ».");
            }
        }

        var matchedOsmIds = new HashSet<long>();
        var coefficients = new List<WorldPopCoefficient>();
        foreach (var tuikDistrict in input.TuikDistricts)
        {
            if (!osmByName.TryGetValue(tuikDistrict.NameNormalized, out var osmDistrict))
            {
                unmatched.Add($"TÜİK {tuikDistrict.Code} {tuikDistrict.Name} sans ilçe OSM.");
                continue;
            }

            matchedOsmIds.Add(osmDistrict.Id);
            if (!input.WorldpopSums.TryGetValue(osmDistrict.Id, out var worldpop))
            {
                failures.Add($"WorldPop : aucune somme pour l'ilçe {osmDistrict.Name}.");
                continue;
            }

            if (worldpop.Sum <= 0)
            {
                failures.Add(
                    $"WorldPop : somme nulle pour l'ilçe {osmDistrict.Name} " +
                    $"({worldpop.NeighborhoodCount} mahalles) ; recalage impossible.");
                continue;
            }

            var coefficient = tuikDistrict.Population / worldpop.Sum;
            coefficients.Add(new WorldPopCoefficient(
                osmDistrict.Id,
                osmDistrict.Name,
                tuikDistrict.Code,
                tuikDistrict.Name,
                worldpop.Sum,
                tuikDistrict.Population,
                coefficient,
                coefficient < MinimumCoefficient || coefficient > MaximumCoefficient,
                worldpop.NeighborhoodCount,
                worldpop.ZeroNeighborhoodCount));
        }

        foreach (var osmDistrict in input.OsmDistricts)
        {
            if (!matchedOsmIds.Contains(osmDistrict.Id))
            {
                unmatched.Add($"OSM {osmDistrict.Name} sans ilçe TÜİK.");
            }
        }

        if (unmatched.Count > 0)
        {
            failures.Add($"{unmatched.Count} ilçe(s) sans correspondance TÜİK/OSM.");
        }

        return new WorldPopPlan(coefficients, unmatched, failures);
    }
}
