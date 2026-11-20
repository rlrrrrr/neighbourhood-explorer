namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class AdminAccounts(IConfiguration configuration)
{
    public const string RoleName = "admin";

    private readonly IReadOnlySet<string> emails = Parse(configuration["ADMIN_EMAILS"]);

    public bool IsAdminEmail(string? email) =>
        email is not null && emails.Contains(AccountInputs.NormalizeEmail(email));

    public static IReadOnlySet<string> Parse(string? configuredEmails) =>
        (configuredEmails ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AccountInputs.NormalizeEmail)
            .Where(email => email.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
