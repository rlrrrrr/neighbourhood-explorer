namespace NeighbourhoodExplorer.Api.Features.Accounts.Contracts;

public sealed record ConfirmEmailRequest(string? UserId, string? Token);
