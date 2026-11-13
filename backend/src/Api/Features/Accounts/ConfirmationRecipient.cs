namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed record ConfirmationRecipient(
    string Email,
    string UserId,
    string DisplayName,
    string Locale);
