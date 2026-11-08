using System.Xml.Linq;

namespace NeighbourhoodExplorer.Etl.Tuik;

public sealed record TuikDistrict(string Code, string Name);

public static class TuikCodelistParser
{
    private static readonly XNamespace Structure =
        "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure";

    private static readonly XNamespace Common =
        "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common";

    public static IReadOnlyList<TuikDistrict> DistrictsOfProvince(XDocument document, string provinceCode)
    {
        var codelist = document.Descendants(Structure + "Codelist").FirstOrDefault()
            ?? throw new EtlFailureException("TÜİK : codelist introuvable dans la réponse.");

        if ((string?)codelist.Attribute("id") != "CL_IBBS"
            || (string?)codelist.Attribute("version") != "1.2")
        {
            throw new EtlFailureException("TÜİK : codelist inattendue (CL_IBBS 1.2 attendue).");
        }

        var districts = new List<TuikDistrict>();
        foreach (var code in codelist.Elements(Structure + "Code"))
        {
            var parent = code.Element(Structure + "Parent")?.Element("Ref")?.Attribute("id")?.Value;
            if (!string.Equals(parent, provinceCode, StringComparison.Ordinal))
            {
                continue;
            }

            var id = (string?)code.Attribute("id")
                ?? throw new EtlFailureException("TÜİK : code sans identifiant dans CL_IBBS 1.2.");
            var name = code.Elements(Common + "Name")
                .FirstOrDefault(element => (string?)element.Attribute(XNamespace.Xml + "lang") == "tr")?.Value
                ?? throw new EtlFailureException($"TÜİK : nom turc manquant pour le code {id}.");

            districts.Add(new TuikDistrict(id, name));
        }

        if (districts.Count == 0)
        {
            throw new EtlFailureException($"TÜİK : aucun ilçe enfant de {provinceCode} dans CL_IBBS 1.2.");
        }

        return districts.OrderBy(district => district.Code, StringComparer.Ordinal).ToList();
    }
}
