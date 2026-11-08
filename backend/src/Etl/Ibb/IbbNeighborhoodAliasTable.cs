using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeighbourhoodExplorer.Etl.Ibb;

public sealed record IbbNeighborhoodAlias(
    [property: JsonPropertyName("district")] string District,
    [property: JsonPropertyName("neighborhood")] string Neighborhood,
    [property: JsonPropertyName("osmNeighborhood")] string OsmNeighborhood);

public sealed class IbbNeighborhoodAliasTable
{
    private readonly IReadOnlyDictionary<(string District, string Neighborhood), string> aliases;

    private IbbNeighborhoodAliasTable(
        IReadOnlyDictionary<(string District, string Neighborhood), string> aliases,
        IReadOnlyCollection<IbbNeighborhoodAlias> all)
    {
        this.aliases = aliases;
        All = all;
    }

    public IReadOnlyCollection<IbbNeighborhoodAlias> All { get; }

    public static IbbNeighborhoodAliasTable Load(string path)
    {
        using var stream = File.OpenRead(path);
        var entries = JsonSerializer.Deserialize<List<IbbNeighborhoodAlias>>(stream)
            ?? throw new InvalidOperationException($"table d'alias vide : {path}.");
        return From(entries);
    }

    public static IbbNeighborhoodAliasTable From(IEnumerable<IbbNeighborhoodAlias> entries)
    {
        var aliases = new Dictionary<(string District, string Neighborhood), string>();
        var all = new List<IbbNeighborhoodAlias>();
        foreach (var entry in entries)
        {
            if (entry.District != IbbNameNormalizer.NormalizeDistrict(entry.District)
                || entry.Neighborhood != IbbNameNormalizer.NormalizeNeighborhood(entry.Neighborhood)
                || entry.OsmNeighborhood != IbbNameNormalizer.NormalizeNeighborhood(entry.OsmNeighborhood))
            {
                throw new InvalidOperationException(
                    $"alias İBB non normalisé : {entry.District} / {entry.Neighborhood} -> " +
                    $"{entry.OsmNeighborhood}.");
            }

            if (!aliases.TryAdd((entry.District, entry.Neighborhood), entry.OsmNeighborhood))
            {
                throw new InvalidOperationException(
                    $"alias İBB en double : {entry.District} / {entry.Neighborhood}.");
            }

            all.Add(entry);
        }

        return new IbbNeighborhoodAliasTable(
            aliases,
            all
                .OrderBy(alias => alias.District, StringComparer.Ordinal)
                .ThenBy(alias => alias.Neighborhood, StringComparer.Ordinal)
                .ToList());
    }

    public bool TryResolve(string district, string neighborhood, out string osmNeighborhood) =>
        aliases.TryGetValue((district, neighborhood), out osmNeighborhood!);
}
