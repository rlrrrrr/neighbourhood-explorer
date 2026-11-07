using System.Diagnostics;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record WorldPopCommandResult(int ExitCode, string StandardOutput, string StandardError);

public static class WorldPopCommandRunner
{
    public static async Task<WorldPopCommandResult> RunAsync(
        WorldPopCommand command,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(command, redirectStandardOutput: true);
        using var process = Start(startInfo, command.FileName);

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new WorldPopCommandResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    public static async Task<WorldPopCommandResult> RunToFileAsync(
        WorldPopCommand command,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(command, redirectStandardOutput: true);
        using var process = Start(startInfo, command.FileName);

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await using (var output = File.Create(outputPath))
            {
                await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new WorldPopCommandResult(
            process.ExitCode,
            string.Empty,
            await errorTask);
    }

    private static ProcessStartInfo CreateStartInfo(WorldPopCommand command, bool redirectStandardOutput)
    {
        var startInfo = new ProcessStartInfo(command.FileName)
        {
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = true,
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (command.Environment is not null)
        {
            foreach (var (name, value) in command.Environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        return startInfo;
    }

    private static Process Start(ProcessStartInfo startInfo, string fileName)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new EtlFailureException($"impossible de démarrer {fileName}.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new EtlFailureException(
                $"{fileName} est absent du système : lancer l'ETL dans l'image Docker (service etl).");
        }
    }

    public static string Tail(string value, int lineCount = 40)
    {
        var lines = value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" | ", lines.TakeLast(lineCount));
    }
}
