namespace NeighbourhoodExplorer.Etl.Transport;

public static class TransportModeExtensions
{
    public static string ToWireValue(this TransportMode mode) => mode switch
    {
        TransportMode.Bus => "bus",
        TransportMode.Rail => "rail",
        TransportMode.Ferry => "ferry",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
