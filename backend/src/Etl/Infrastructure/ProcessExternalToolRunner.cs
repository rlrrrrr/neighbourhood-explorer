using System.Diagnostics;

namespace NeighbourhoodExplorer.Etl.Infrastructure;

public sealed class ProcessExternalToolRunner : IExternalToolRunner
{
    public async Task<ExternalToolResult> RunAsync(
        ExternalToolCommand command,
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

        return new ExternalToolResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    public async Task<ExternalToolResult> RunToFileAsync(
        ExternalToolCommand command,
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

        return new ExternalToolResult(
            process.ExitCode,
            string.Empty,
            await errorTask);
    }

    public async Task<ExternalToolResult> RunWithDiagnosticsLogAsync(
        ExternalToolCommand command,
        string diagnosticsPath,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(command, redirectStandardOutput: true);
        using var process = Start(startInfo, command.FileName);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        try
        {
            await using (var diagnostics = File.Create(diagnosticsPath))
            {
                await process.StandardError.BaseStream.CopyToAsync(diagnostics, cancellationToken);
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ExternalToolResult(
            process.ExitCode,
            await outputTask,
            await ReadTailAsync(diagnosticsPath, cancellationToken));
    }

    private static async Task<string> ReadTailAsync(string path, CancellationToken cancellationToken)
    {
        const int tailBytes = 8192;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        var start = Math.Max(0, stream.Length - tailBytes);
        stream.Seek(start, SeekOrigin.Begin);

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static ProcessStartInfo CreateStartInfo(
        ExternalToolCommand command,
        bool redirectStandardOutput)
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
}
