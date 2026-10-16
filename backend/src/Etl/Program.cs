var dataDirectory = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";

if (args.Length != 1)
{
    Console.Error.WriteLine("ETL: un argument obligatoire : nom du sous-répertoire dans data/raw/.");
    Console.Error.WriteLine("ETL: usage: etl <source>");
    return 2;
}

var sourceName = args[0];
var sourceDirectory = Path.Combine(dataDirectory, "raw", sourceName);

if (!Directory.Exists(sourceDirectory))
{
    var available = Directory.Exists(Path.Combine(dataDirectory, "raw"))
        ? string.Join(", ", Directory.GetDirectories(Path.Combine(dataDirectory, "raw"))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal))
        : "(aucun)";
    Console.Error.WriteLine($"ETL: source introuvable: {sourceName}. Sources disponibles: {available}");
    return 2;
}

var sourceFiles = Directory
    .GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
    .Where(path => !Path.GetFileName(path).StartsWith('.'))
    .Order(StringComparer.Ordinal)
    .ToArray();

if (sourceFiles.Length == 0)
{
    Console.WriteLine($"ETL: aucun fichier dans {sourceDirectory} — rien à charger.");
    return 0;
}

Console.Error.WriteLine(
    $"ETL: {sourceFiles.Length} fichier(s) pour la source '{sourceName}', mais le chargeur PostGIS n'est pas " +
    "encore implémenté (phase ETL, voir docs/data.md).");
return 1;
