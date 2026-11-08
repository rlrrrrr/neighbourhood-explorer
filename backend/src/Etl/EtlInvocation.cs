namespace NeighbourhoodExplorer.Etl;

public sealed class EtlUsageException(string message) : Exception(message);

public sealed class EtlFailureException(string message) : Exception(message);

public sealed record EtlInvocation(string SourceId, string? ProvinceCode);

public static class EtlInvocationParser
{
    public static EtlInvocation Parse(IReadOnlyList<string> arguments)
    {
        var positional = new List<string>();
        string? provinceCode = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(argument);
                continue;
            }

            var (name, value) = ReadOption(arguments, ref index);
            provinceCode = name switch
            {
                "province" => value,
                _ => throw new EtlUsageException($"option inconnue : --{name}"),
            };
        }

        if (positional.Count != 1)
        {
            throw new EtlUsageException("usage : etl <source> [--province=TR-34]");
        }

        return new EtlInvocation(positional[0], provinceCode);
    }

    private static (string Name, string Value) ReadOption(IReadOnlyList<string> arguments, ref int index)
    {
        var argument = arguments[index];
        var separatorIndex = argument.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            return (argument[2..separatorIndex], argument[(separatorIndex + 1)..]);
        }

        var name = argument[2..];
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new EtlUsageException($"option --{name} sans valeur");
        }

        index++;
        return (name, arguments[index]);
    }
}

public sealed record EtlSettings(
    string DataDirectory,
    string? ConnectionString,
    string? TuikApiKey)
{
    public static EtlSettings FromEnvironment()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("DATA_DIR");
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        var tuikApiKey = Environment.GetEnvironmentVariable("TUIK_API_KEY");

        return new EtlSettings(
            string.IsNullOrWhiteSpace(dataDirectory) ? "/data" : dataDirectory,
            string.IsNullOrWhiteSpace(connectionString) ? null : connectionString,
            string.IsNullOrWhiteSpace(tuikApiKey) ? null : tuikApiKey);
    }
}
