namespace NeighbourhoodExplorer.Api.Features.Accounts.Contracts;

public sealed record MeResponse(
    string UserId,
    string Email,
    string DisplayName,
    string Locale,
    bool IsDemo,
    bool IsAdmin);
