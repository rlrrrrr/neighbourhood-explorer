namespace NeighbourhoodExplorer.Etl.Ibb;

public sealed record IbbNeighborhoodAlias(string District, string Neighborhood, string OsmNeighborhood);

public static class IbbNeighborhoodAliases
{
    private static readonly IReadOnlyDictionary<(string District, string Neighborhood), string> Aliases =
        new Dictionary<(string District, string Neighborhood), string>
        {
            [("kadikoy", "19.may")] = "19 mayis",
            [("buyukcekmece", "19.may")] = "19 mayis",
            [("sisli", "19.may")] = "19 mayis",
            [("beyoglu", "piripasa")] = "piri pasa",
            [("sariyer", "rumeli hisari")] = "rumelihisari",
            [("sariyer", "rumeli kavagi")] = "rumelikavagi",
            [("kagithane", "emniyet evleri")] = "emniyetevleri",
            [("kartal", "topselvi")] = "topselvi mahalesi",
            [("uskudar", "kucukcamlica")] = "kucuk camlica",
            [("arnavutkoy", "nenehatun")] = "nene hatun",
            [("avcilar", "mustafa kemalpasa")] = "mustafa kemal pasa",
            [("beyoglu", "katipmustafa celebi")] = "katip mustafa celebi",
            [("beyoglu", "kemankes karamustafapasa")] = "kemankes karamustafa pasa",
            [("beyoglu", "mueyyetzade")] = "mueyyedzade",
            [("sile", "degirmencayiri")] = "degirmen cayiri",
            [("beykoz", "anadolu feneri")] = "anadolufeneri",
            [("beykoz", "cumhuriyet")] = "cumhuriyetkoy",
            [("beykoz", "ciftlik")] = "cavusbasi ciftlik",
            [("buyukcekmece", "mimar sinan merkez")] = "mimarsinan",
            // Renommages historiques et noms préfixés de Silivri (arbitrage du 2026-11-05).
            [("bagcilar", "evren")] = "15 temmuz",
            [("bayrampasa", "ortamahalle")] = "orta",
            [("esenler", "havaalani")] = "atisalani",
            [("sariyer", "sariyer merkez")] = "merkez",
            [("silivri", "balaban")] = "canta balaban",
            [("silivri", "fevzipasa")] = "degirmenkoy fevzipasa",
            [("silivri", "hurriyet")] = "kavakli hurriyet",
            [("silivri", "ismetpasa")] = "degirmenkoy ismetpasa",
            [("silivri", "kavakli")] = "kavakli istiklal",
            [("silivri", "sancaktepe")] = "canta sancaktepe",
            [("sile", "kizilca")] = "kizilcakoy",
            [("sisli", "izzet pasa")] = "izzetpasa",
        };

    public static IReadOnlyCollection<IbbNeighborhoodAlias> All { get; } = Aliases
        .Select(alias => new IbbNeighborhoodAlias(alias.Key.District, alias.Key.Neighborhood, alias.Value))
        .OrderBy(alias => alias.District, StringComparer.Ordinal)
        .ThenBy(alias => alias.Neighborhood, StringComparer.Ordinal)
        .ToList();

    public static bool TryResolve(string district, string neighborhood, out string osmNeighborhood) =>
        Aliases.TryGetValue((district, neighborhood), out osmNeighborhood!);
}
