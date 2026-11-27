namespace NeighbourhoodExplorer.Api.Features.Accounts.Contracts;

public sealed record RegisterRequest(string? Email, string? DisplayName, string? Password, string? Locale);
