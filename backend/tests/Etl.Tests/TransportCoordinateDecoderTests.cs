using NeighbourhoodExplorer.Etl.Transport;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TransportCoordinateDecoderTests
{
    [Fact]
    public void Decodes_the_dotted_iett_format()
    {
        Assert.True(TransportCoordinateDecoder.TryDecodeIettCoordinate(
            "410.191.700.005.564",
            out var latitude));
        Assert.Equal(41.0191700005564, latitude, 12);

        Assert.True(TransportCoordinateDecoder.TryDecodeIettCoordinate(
            "286.843.529.999.755",
            out var longitude));
        Assert.Equal(28.6843529999755, longitude, 12);
    }

    [Fact]
    public void Decodes_values_with_fewer_digits()
    {
        Assert.True(TransportCoordinateDecoder.TryDecodeIettCoordinate(
            "41.163.664.999",
            out var latitude));
        Assert.Equal(41.163664999, latitude, 9);
    }

    [Theory]
    [InlineData("4,12E+14")]
    [InlineData("direction: GİDİŞ")]
    [InlineData("")]
    [InlineData("0")]
    public void Rejects_malformed_export_values(string raw)
    {
        Assert.False(TransportCoordinateDecoder.TryDecodeIettCoordinate(raw, out _));
    }

    [Theory]
    [InlineData(28.97, 41.01, true)]
    [InlineData(29.13, 40.87, true)]
    [InlineData(41.21, 41.01, false)]
    [InlineData(28.97, 39.00, false)]
    public void Checks_the_istanbul_envelope(double longitude, double latitude, bool expected)
    {
        Assert.Equal(expected, TransportCoordinateDecoder.IsInsideIstanbul(longitude, latitude));
    }
}
