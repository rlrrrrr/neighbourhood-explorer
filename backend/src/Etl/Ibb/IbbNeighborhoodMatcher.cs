namespace NeighbourhoodExplorer.Etl.Ibb;

public sealed record OsmNeighborhood(
    long AdminAreaId,
    string DistrictNormalized,
    string NeighborhoodNormalized,
    string NeighborhoodName);

public sealed record IbbMatchRow(
    IbbBuildingRow Row,
    long? AdminAreaId,
    string? MatchMethod);

public sealed record IbbUnmatchedRow(
    string District,
    string Neighborhood,
    long MahalleUavt,
    IReadOnlyList<string> Candidates);

public sealed record IbbAliasMatch(
    string District,
    string Neighborhood,
    string OsmNeighborhood);

public sealed record IbbMatchResult(
    IReadOnlyList<IbbMatchRow> Rows,
    IReadOnlyList<IbbUnmatchedRow> Unmatched,
    IReadOnlyList<IbbAliasMatch> AliasMatches);

public static class IbbNeighborhoodMatcher
{
    public static IbbMatchResult Match(
        IReadOnlyList<IbbBuildingRow> rows,
        IReadOnlyList<OsmNeighborhood> osmNeighborhoods)
    {
        var byKey = new Dictionary<(string District, string Neighborhood), OsmNeighborhood>();
        foreach (var neighborhood in osmNeighborhoods.OrderBy(neighborhood => neighborhood.AdminAreaId))
        {
            byKey.TryAdd(
                (neighborhood.DistrictNormalized, neighborhood.NeighborhoodNormalized),
                neighborhood);
        }

        var matchedIds = new HashSet<long>();
        var matchRows = new List<IbbMatchRow>(rows.Count);
        var unmatched = new List<IbbUnmatchedRow>();
        var aliasMatches = new List<IbbAliasMatch>();

        foreach (var row in rows)
        {
            var key = (row.DistrictNormalized, row.NeighborhoodNormalized);
            if (byKey.TryGetValue(key, out var match) && !matchedIds.Contains(match.AdminAreaId))
            {
                matchedIds.Add(match.AdminAreaId);
                matchRows.Add(new IbbMatchRow(row, match.AdminAreaId, "name"));
                continue;
            }

            if (IbbNeighborhoodAliases.TryResolve(key.DistrictNormalized, key.NeighborhoodNormalized, out var alias)
                && byKey.TryGetValue((key.DistrictNormalized, alias), out var aliasMatch)
                && !matchedIds.Contains(aliasMatch.AdminAreaId))
            {
                matchedIds.Add(aliasMatch.AdminAreaId);
                matchRows.Add(new IbbMatchRow(row, aliasMatch.AdminAreaId, "alias"));
                aliasMatches.Add(new IbbAliasMatch(
                    row.DistrictName,
                    row.NeighborhoodName,
                    aliasMatch.NeighborhoodName));
                continue;
            }

            matchRows.Add(new IbbMatchRow(row, null, null));
            unmatched.Add(new IbbUnmatchedRow(
                row.DistrictName,
                row.NeighborhoodName,
                row.MahalleUavt,
                Array.Empty<string>()));
        }

        if (unmatched.Count == 0)
        {
            return new IbbMatchResult(matchRows, unmatched, aliasMatches);
        }

        var candidatesByDistrict = osmNeighborhoods
            .Where(neighborhood => !matchedIds.Contains(neighborhood.AdminAreaId))
            .GroupBy(neighborhood => neighborhood.DistrictNormalized, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(neighborhood => neighborhood.NeighborhoodName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        var withCandidates = unmatched
            .Select(row =>
            {
                var district = IbbNameNormalizer.NormalizeDistrict(row.District);
                return row with
                {
                    Candidates = candidatesByDistrict.TryGetValue(district, out var candidates)
                        ? candidates
                        : Array.Empty<string>(),
                };
            })
            .ToList();

        return new IbbMatchResult(matchRows, withCandidates, aliasMatches);
    }
}
