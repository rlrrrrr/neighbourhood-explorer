using NeighbourhoodExplorer.Etl.Infrastructure;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class ProcessExternalToolRunnerTests
{
    [Fact]
    public async Task RunWithDiagnosticsLogAsync_captures_standard_error_in_the_log_file()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"etl-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var diagnosticsPath = Path.Combine(directory, "diagnostics.log");

        try
        {
            var runner = new ProcessExternalToolRunner();
            var result = await runner.RunWithDiagnosticsLogAsync(
                new ExternalToolCommand(
                    "/bin/sh",
                    ["-c", "printf 'progress-line' >&2; printf 'result-line'"]),
                diagnosticsPath,
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("result-line", result.StandardOutput);
            Assert.Contains("progress-line", result.StandardError);
            Assert.Contains("progress-line", await File.ReadAllTextAsync(diagnosticsPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
