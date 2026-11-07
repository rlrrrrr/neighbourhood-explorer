using System.Security.Cryptography;
using System.Text;

namespace NeighbourhoodExplorer.Etl.Tuik;

public static class TuikCanonicalChecksum
{
    public static string Compute(IReadOnlyDictionary<string, int> values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(
                '\n',
                values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}")))));
}
