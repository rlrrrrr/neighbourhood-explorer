using System.Security.Cryptography;

namespace NeighbourhoodExplorer.Etl.Data;

public static class Sha256Hasher
{
    public static async Task<string> ComputeFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}

public static class SourceFileLocator
{
    public static string FindSingle(string dataDirectory, string sourceId, string searchPattern)
    {
        var directory = Path.Combine(dataDirectory, "raw", sourceId);
        if (!Directory.Exists(directory))
        {
            throw new EtlFailureException($"répertoire de données brutes introuvable : {directory}");
        }

        var files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
        if (files.Length != 1)
        {
            throw new EtlFailureException(
                $"{directory} doit contenir exactement un fichier « {searchPattern} » (trouvé : {files.Length}).");
        }

        return files[0];
    }
}
