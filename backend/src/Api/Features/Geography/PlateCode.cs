using System.Globalization;

namespace NeighbourhoodExplorer.Api.Features.Geography;

public readonly record struct PlateCode
{
    private const int Minimum = 1;
    private const int Maximum = 81;

    private PlateCode(int value) => Value = value;

    public int Value { get; }

    public static bool TryCreate(int value, out PlateCode plateCode)
    {
        if (value is < Minimum or > Maximum)
        {
            plateCode = default;
            return false;
        }

        plateCode = new PlateCode(value);
        return true;
    }

    public static bool TryParseOfficialCode(string? officialCode, out PlateCode plateCode)
    {
        plateCode = default;
        if (officialCode is null || !officialCode.StartsWith("TR-", StringComparison.Ordinal))
        {
            return false;
        }

        var digits = officialCode[3..];
        if (digits.Length is < 1 or > 2 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        return int.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value)
            && TryCreate(value, out plateCode);
    }

    public string ToOfficialCode() =>
        $"TR-{Value.ToString("D2", CultureInfo.InvariantCulture)}";
}
