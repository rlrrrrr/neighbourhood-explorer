using System.Security.Cryptography;
using System.Text;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class TransportBundleChecksum
{
    public static string Compute(IReadOnlyDictionary<string, string> fileChecksums) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(
                '\n',
                fileChecksums
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}")))));
}
