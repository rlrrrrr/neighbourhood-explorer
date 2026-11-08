using System.Globalization;
using System.Xml.Linq;

namespace NeighbourhoodExplorer.Etl.Tuik;

public static class TuikAdnksParser
{
    private static readonly XNamespace Generic =
        "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic";

    private static readonly XNamespace Message =
        "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message";

    public static IReadOnlyDictionary<string, int> Parse(XDocument document, int referenceYear)
    {
        var root = document.Root;
        if (root?.Name != Message + "GenericData")
        {
            throw new EtlFailureException(
                $"TÜİK : format SDMX inattendu (racine « {root?.Name.LocalName ?? "absente"} »).");
        }

        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var series in root.Descendants(Generic + "Series"))
        {
            var key = series.Element(Generic + "SeriesKey")
                ?.Elements(Generic + "Value")
                .ToDictionary(
                    value => (string)value.Attribute("id")!,
                    value => (string)value.Attribute("value")!)
                ?? throw new EtlFailureException("TÜİK : SeriesKey manquante dans la réponse.");

            if (!MatchesSeries(key))
            {
                continue;
            }

            var observation = series.Element(Generic + "Obs")
                ?? throw new EtlFailureException("TÜİK : observation manquante dans la réponse.");
            var period = (string?)observation.Element(Generic + "ObsDimension")?.Attribute("value");
            if (!string.Equals(
                    period,
                    referenceYear.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                continue;
            }

            var raw = (string?)observation.Element(Generic + "ObsValue")?.Attribute("value")
                ?? throw new EtlFailureException("TÜİK : valeur d'observation manquante.");
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var population))
            {
                throw new EtlFailureException($"TÜİK : population non entière « {raw} ».");
            }

            if (!values.TryAdd(key["IKAMET_YERI"], population))
            {
                throw new EtlFailureException(
                    $"TÜİK : plusieurs séries pour IKAMET_YERI={key["IKAMET_YERI"]}.");
            }
        }

        return values;
    }

    private static bool MatchesSeries(IReadOnlyDictionary<string, string> key) =>
        key.GetValueOrDefault("REF_AREA") == "TR"
        && key.GetValueOrDefault("FREQ") == "A"
        && key.GetValueOrDefault("INDICATOR") == "NG_ADNKS"
        && key.GetValueOrDefault("YERLESIM_YERI_TUR") == "_T"
        && key.GetValueOrDefault("ADNKS_GOSTERGE") == "NUFUS";
}
