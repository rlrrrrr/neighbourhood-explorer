namespace NeighbourhoodExplorer.Etl.Infrastructure;

public interface IEtlConsole
{
    void WriteLine(string message);
}

public sealed class TextWriterEtlConsole(TextWriter writer) : IEtlConsole
{
    public void WriteLine(string message) => writer.WriteLine(message);
}
