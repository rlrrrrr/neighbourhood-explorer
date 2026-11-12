using System.Globalization;
using System.Text;

namespace NeighbourhoodExplorer.Etl;

public static class TurkishText
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static string Normalize(string value)
    {
        var lowered = value.ToLower(TurkishCulture).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(lowered.Length);
        foreach (var character in lowered)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(FoldTurkishLetter(character));
        }

        return CollapseWhitespace(builder.ToString());
    }

    public static string SqlNormalizeExpression(string column) => $"""
        translate(
            regexp_replace(
                lower(translate(btrim({column}), 'İI', 'iı')),
                '\s+', ' ', 'g'),
            'ışğçöüâîûêô', 'isgcouaiueo')
        """;

    private static char FoldTurkishLetter(char character) => character switch
    {
        'ı' => 'i',
        'ş' => 's',
        'ğ' => 'g',
        'ç' => 'c',
        'ö' => 'o',
        'ü' => 'u',
        'â' => 'a',
        'î' => 'i',
        'û' => 'u',
        'ê' => 'e',
        'ô' => 'o',
        _ => character,
    };

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
