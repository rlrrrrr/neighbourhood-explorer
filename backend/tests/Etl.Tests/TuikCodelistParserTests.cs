using System.Xml.Linq;
using NeighbourhoodExplorer.Etl.Tuik;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TuikCodelistParserTests
{
    [Fact]
    public void DistrictsOfProvince_keeps_only_the_children_of_the_province()
    {
        var districts = TuikCodelistParser.DistrictsOfProvince(XDocument.Parse(Fixture), "TR100");

        Assert.Equal(2, districts.Count);
        Assert.Equal(new TuikDistrict("1103", "Adalar"), districts[0]);
        Assert.Equal(new TuikDistrict("1183", "Beşiktaş"), districts[1]);
    }

    [Fact]
    public void DistrictsOfProvince_rejects_an_unexpected_codelist_version()
    {
        var document = XDocument.Parse(Fixture.Replace("version=\"1.2\"", "version=\"1.3\"", StringComparison.Ordinal));

        var exception = Assert.Throws<EtlFailureException>(
            () => TuikCodelistParser.DistrictsOfProvince(document, "TR100"));

        Assert.Contains("CL_IBBS 1.2", exception.Message);
    }

    private const string Fixture = """
        <?xml version="1.0" encoding="utf-8"?>
        <message:Structure
            xmlns:message="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message"
            xmlns:structure="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure"
            xmlns:common="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">
          <message:Structures>
            <structure:Codelists>
              <structure:Codelist id="CL_IBBS" version="1.2">
                <structure:Code id="TR100">
                  <common:Name xml:lang="tr">İstanbul</common:Name>
                </structure:Code>
                <structure:Code id="TR10">
                  <common:Name xml:lang="tr">İstanbul (NUTS)</common:Name>
                  <structure:Parent>
                    <Ref id="TR1" />
                  </structure:Parent>
                </structure:Code>
                <structure:Code id="1183">
                  <common:Name xml:lang="tr">Beşiktaş</common:Name>
                  <structure:Parent>
                    <Ref id="TR100" />
                  </structure:Parent>
                </structure:Code>
                <structure:Code id="1103">
                  <common:Name xml:lang="tr">Adalar</common:Name>
                  <structure:Parent>
                    <Ref id="TR100" />
                  </structure:Parent>
                </structure:Code>
              </structure:Codelist>
            </structure:Codelists>
          </message:Structures>
        </message:Structure>
        """;
}
