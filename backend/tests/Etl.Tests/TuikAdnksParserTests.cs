using System.Xml.Linq;
using NeighbourhoodExplorer.Etl.Tuik;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TuikAdnksParserTests
{
    [Fact]
    public void Parse_keeps_only_the_adnks_population_series_for_the_year()
    {
        var values = TuikAdnksParser.Parse(XDocument.Parse(Fixture), 2025);

        Assert.Equal(2, values.Count);
        Assert.Equal(17489, values["1103"]);
        Assert.Equal(15754053, values["TR100"]);
    }

    [Fact]
    public void Parse_ignores_another_reference_year()
    {
        var values = TuikAdnksParser.Parse(XDocument.Parse(Fixture), 2024);

        Assert.Empty(values);
    }

    [Fact]
    public void Parse_rejects_a_non_sdmx_document()
    {
        var document = XDocument.Parse("<html><body>quota</body></html>");

        var exception = Assert.Throws<EtlFailureException>(
            () => TuikAdnksParser.Parse(document, 2025));

        Assert.Contains("format SDMX", exception.Message);
    }

    private const string Fixture = """
        <?xml version="1.0" encoding="utf-8"?>
        <message:GenericData
            xmlns:message="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message"
            xmlns:generic="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic">
          <message:DataSet>
            <generic:Series>
              <generic:SeriesKey>
                <generic:Value id="REF_AREA" value="TR" />
                <generic:Value id="FREQ" value="A" />
                <generic:Value id="INDICATOR" value="NG_ADNKS" />
                <generic:Value id="YERLESIM_YERI_TUR" value="_T" />
                <generic:Value id="IKAMET_YERI" value="1103" />
                <generic:Value id="ADNKS_GOSTERGE" value="NUFUS" />
              </generic:SeriesKey>
              <generic:Obs>
                <generic:ObsDimension id="TIME_PERIOD" value="2025" />
                <generic:ObsValue value="17489" />
              </generic:Obs>
            </generic:Series>
            <generic:Series>
              <generic:SeriesKey>
                <generic:Value id="REF_AREA" value="TR" />
                <generic:Value id="FREQ" value="A" />
                <generic:Value id="INDICATOR" value="NG_ADNKS" />
                <generic:Value id="YERLESIM_YERI_TUR" value="_T" />
                <generic:Value id="IKAMET_YERI" value="TR100" />
                <generic:Value id="ADNKS_GOSTERGE" value="NUFUS" />
              </generic:SeriesKey>
              <generic:Obs>
                <generic:ObsDimension id="TIME_PERIOD" value="2025" />
                <generic:ObsValue value="15754053" />
              </generic:Obs>
            </generic:Series>
            <generic:Series>
              <generic:SeriesKey>
                <generic:Value id="REF_AREA" value="TR" />
                <generic:Value id="FREQ" value="A" />
                <generic:Value id="INDICATOR" value="NG_ADNKS" />
                <generic:Value id="YERLESIM_YERI_TUR" value="_T" />
                <generic:Value id="IKAMET_YERI" value="1103" />
                <generic:Value id="ADNKS_GOSTERGE" value="ORTALAMA_HANE_BUYUKLUGU" />
              </generic:SeriesKey>
              <generic:Obs>
                <generic:ObsDimension id="TIME_PERIOD" value="2025" />
                <generic:ObsValue value="3" />
              </generic:Obs>
            </generic:Series>
          </message:DataSet>
        </message:GenericData>
        """;
}
