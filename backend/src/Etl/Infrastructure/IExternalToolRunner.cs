namespace NeighbourhoodExplorer.Etl.Infrastructure;

public sealed record ExternalToolCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record ExternalToolResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Tail(int lineCount = 40)
    {
        var lines = StandardError
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" | ", lines.TakeLast(lineCount));
    }
}

public interface IExternalToolRunner
{
    Task<ExternalToolResult> RunAsync(
        ExternalToolCommand command,
        CancellationToken cancellationToken);

    Task<ExternalToolResult> RunToFileAsync(
        ExternalToolCommand command,
        string outputPath,
        CancellationToken cancellationToken);
}
