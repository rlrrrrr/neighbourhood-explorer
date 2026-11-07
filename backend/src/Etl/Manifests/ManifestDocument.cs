using System.Text;

namespace NeighbourhoodExplorer.Etl.Manifests;

public sealed class ManifestException(string message) : Exception(message);

public sealed class ManifestDocument
{
    private readonly IReadOnlyDictionary<string, object?> values;
    private readonly string path;

    private ManifestDocument(IReadOnlyDictionary<string, object?> values, string path)
    {
        this.values = values;
        this.path = path;
    }

    public static ManifestDocument Load(string path) => Parse(File.ReadAllLines(path), path);

    public static ManifestDocument Parse(IReadOnlyList<string> lines, string path)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var index = 0;

        while (index < lines.Count)
        {
            var line = lines[index];
            if (IsIgnorable(line))
            {
                index++;
                continue;
            }

            if (CountIndent(line) != 0)
            {
                throw Error(path, index, "indentation inattendue");
            }

            var (key, rawValue) = SplitKeyValue(path, index, line);
            if (values.ContainsKey(key))
            {
                throw Error(path, index, $"clé dupliquée « {key} »");
            }

            index++;
            if (rawValue.Length > 0)
            {
                values[key] = ParseValue(path, index - 1, rawValue);
                continue;
            }

            var block = new List<(string Line, int Number)>();
            while (index < lines.Count)
            {
                var candidate = lines[index];
                if (IsIgnorable(candidate))
                {
                    index++;
                    continue;
                }

                if (CountIndent(candidate) == 0)
                {
                    break;
                }

                block.Add((candidate, index));
                index++;
            }

            values[key] = ParseBlock(path, block);
        }

        return new ManifestDocument(values, path);
    }

    public string? Text(string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            null => null,
            string text => text,
            _ => throw new ManifestException($"{path}: « {key} » doit être une valeur simple."),
        };
    }

    public IReadOnlyList<string> List(string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            null => [],
            List<string> list => list,
            _ => throw new ManifestException($"{path}: « {key} » doit être une liste."),
        };
    }

    public IReadOnlyDictionary<string, string> Map(string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return value switch
        {
            null => new Dictionary<string, string>(StringComparer.Ordinal),
            Dictionary<string, string> map => map,
            _ => throw new ManifestException($"{path}: « {key} » doit être une table de valeurs simples."),
        };
    }

    private static object? ParseBlock(string path, List<(string Line, int Number)> block)
    {
        if (block.Count == 0)
        {
            return null;
        }

        var first = block[0].Line.TrimStart();
        if (first == "-" || first.StartsWith("- ", StringComparison.Ordinal))
        {
            var items = new List<string>();
            foreach (var (line, number) in block)
            {
                var trimmed = line.TrimStart();
                if (trimmed != "-" && !trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    throw Error(path, number, "élément de liste attendu");
                }

                var item = StripComment(trimmed[1..]).Trim();
                items.Add(ParseScalar(path, number, item) ?? string.Empty);
            }

            return items;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (line, number) in block)
        {
            var (key, rawValue) = SplitKeyValue(path, number, line);
            if (map.ContainsKey(key))
            {
                throw Error(path, number, $"clé dupliquée « {key} »");
            }

            map[key] = ParseScalar(path, number, StripComment(rawValue).Trim()) ?? string.Empty;
        }

        return map;
    }

    private static object? ParseValue(string path, int number, string rawValue)
    {
        var value = StripComment(rawValue).Trim();
        if (!value.StartsWith("[", StringComparison.Ordinal))
        {
            return ParseScalar(path, number, value);
        }

        if (!value.EndsWith(']'))
        {
            throw Error(path, number, "liste en ligne non terminée par « ] »");
        }

        return SplitFlowList(value[1..^1])
            .Select(item => ParseScalar(path, number, item.Trim()) ?? string.Empty)
            .ToList();
    }

    private static IEnumerable<string> SplitFlowList(string value)
    {
        var current = new StringBuilder();
        char? quote = null;

        foreach (var character in value)
        {
            if (quote is not null)
            {
                current.Append(character);
                if (character == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                current.Append(character);
                continue;
            }

            if (character == ',')
            {
                yield return current.ToString();
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0 || value.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static string? ParseScalar(string path, int number, string value)
    {
        if (value.Length == 0 || value == "~" || value == "null")
        {
            return null;
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value;
    }

    private static (string Key, string Value) SplitKeyValue(string path, int number, string line)
    {
        var separatorIndex = FindSeparator(line);
        if (separatorIndex <= 0)
        {
            throw Error(path, number, "ligne attendue au format « clé: valeur »");
        }

        var key = line[..separatorIndex].Trim();
        if (key.Length == 0 || key.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-')))
        {
            throw Error(path, number, $"clé invalide « {key} »");
        }

        return (key, line[(separatorIndex + 1)..].Trim());
    }

    private static int FindSeparator(string line)
    {
        char? quote = null;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quote is not null)
            {
                if (character == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == ':')
            {
                return index;
            }
        }

        return -1;
    }

    private static string StripComment(string line)
    {
        char? quote = null;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quote is not null)
            {
                if (character == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '#' && (index == 0 || char.IsWhiteSpace(line[index - 1])))
            {
                return line[..index];
            }
        }

        return line;
    }

    private static bool IsIgnorable(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length == 0 || trimmed.StartsWith('#');
    }

    private static int CountIndent(string line) => line.Length - line.TrimStart().Length;

    private static ManifestException Error(string path, int zeroBasedLineNumber, string message) =>
        new($"{path}:{zeroBasedLineNumber + 1}: {message}");
}
