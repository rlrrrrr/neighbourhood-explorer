using System.Text;

namespace NeighbourhoodExplorer.Etl.Ibb;

public static class IbbTextRepair
{
    private static readonly Encoding Latin1Strict = Encoding.GetEncoding(
        "ISO-8859-1",
        EncoderFallback.ExceptionFallback,
        DecoderFallback.ExceptionFallback);

    private static readonly Lazy<Encoding> Windows1254 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1254,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    });

    public static string Repair(string value)
    {
        var bytes = Latin1Strict.GetBytes(value);
        return Windows1254.Value.GetString(bytes);
    }
}
