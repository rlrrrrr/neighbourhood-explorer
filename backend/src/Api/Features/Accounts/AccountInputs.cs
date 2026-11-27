namespace NeighbourhoodExplorer.Api.Features.Accounts;

public static class AccountInputs
{
    public const int DisplayNameMaxLength = 80;

    private static readonly HashSet<string> SupportedLocales =
        new(StringComparer.Ordinal) { "fr", "en", "tr", "ar" };

    public static string NormalizeDisplayName(string? displayName) => (displayName ?? string.Empty).Trim();

    public static bool IsValidDisplayName(string displayName) =>
        displayName.Length is > 0 and <= DisplayNameMaxLength;

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Length <= 256
        && email.Count(character => character == '@') == 1
        && email.IndexOf(' ') < 0
        && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);

    public static string NormalizeLocale(string? locale) =>
        locale is not null && SupportedLocales.Contains(locale) ? locale : "fr";

    public static string NormalizeEmail(string email) => email.Trim();
}
