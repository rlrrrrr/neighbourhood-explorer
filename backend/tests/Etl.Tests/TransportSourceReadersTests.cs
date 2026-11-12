using NeighbourhoodExplorer.Etl.Transport;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TransportSourceReadersTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"transport-readers-{Guid.NewGuid():N}");

    public TransportSourceReadersTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Reads_iett_stops_and_rejects_malformed_rows()
    {
        var path = Write("stops.csv",
            """
            stop_id;stop_code;stop_name;stop_desc;stop_lat;stop_lon;location_type;
            1;100;KADIKÖY;direction: A;410.191.700.005.564;286.843.529.999.755;0;
            2;101;MARMARA; İKTİSAT;direction: B;4,10E+13;2,91E+12;0;
            3;102;BOĞAZ;direction: C;410.191.700.005.564;286.843.529.999.755;
            4;103;İSTANBUL DIŞI;direction: D;390.000.000.000.000;286.843.529.999.755;0;
            """);

        var result = IettStopsReader.Read(path);

        var point = Assert.Single(result.Points);
        Assert.Equal(TransportMode.Bus, point.Mode);
        Assert.Equal("KADIKÖY", point.Name);
        Assert.Equal(28.6843529999755, point.Longitude, 12);
        Assert.Equal(41.0191700005564, point.Latitude, 12);
        Assert.Equal(3, result.Rejected);
    }

    [Fact]
    public void Reads_metro_stations_and_rejects_missing_coordinates()
    {
        var path = Write("metro.json",
            """
            {
              "Success": true,
              "Data": [
                {
                  "Name": "YENIKAPI",
                  "DetailInfo": { "Latitude": "41.0055971704", "Longitude": "28.9513306172" }
                },
                {
                  "Name": "VEYSELKARANI",
                  "DetailInfo": { "Latitude": "", "Longitude": "" }
                },
                {
                  "Name": "HORS EMPRISE",
                  "DetailInfo": { "Latitude": "39.0", "Longitude": "28.9" }
                }
              ]
            }
            """);

        var result = MetroStationsReader.Read(path);

        var point = Assert.Single(result.Points);
        Assert.Equal(TransportMode.Rail, point.Mode);
        Assert.Equal("YENIKAPI", point.Name);
        Assert.Equal(28.9513306172, point.Longitude, 10);
        Assert.Equal(2, result.Rejected);
    }

    [Fact]
    public void Reads_ferry_piers_from_kml_in_longitude_latitude_order()
    {
        var path = Write("piers.kml",
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>BEŞİKTAŞ-1</name>
                  <Point><coordinates>29.00734241946241,41.04102185617403,0</coordinates></Point>
                </Placemark>
                <Placemark>
                  <name>SANS COORDONNÉES</name>
                </Placemark>
              </Document>
            </kml>
            """);

        var result = FerryPiersReader.Read(path);

        var point = Assert.Single(result.Points);
        Assert.Equal(TransportMode.Ferry, point.Mode);
        Assert.Equal("BEŞİKTAŞ-1", point.Name);
        Assert.Equal(29.00734241946241, point.Longitude, 10);
        Assert.Equal(41.04102185617403, point.Latitude, 10);
        Assert.Equal(1, result.Rejected);
    }

    [Fact]
    public void Computes_a_stable_bundle_checksum_independent_of_dictionary_order()
    {
        var first = new Dictionary<string, string>
        {
            ["b.json"] = "bbbb",
            ["a.csv"] = "aaaa",
        };
        var second = new Dictionary<string, string>
        {
            ["a.csv"] = "aaaa",
            ["b.json"] = "bbbb",
        };

        Assert.Equal(
            TransportBundleChecksum.Compute(first),
            TransportBundleChecksum.Compute(second));
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
