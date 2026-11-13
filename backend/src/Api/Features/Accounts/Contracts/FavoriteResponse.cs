namespace NeighbourhoodExplorer.Api.Features.Accounts.Contracts;

public sealed record FavoriteResponse(
    string TargetType,
    string TargetId,
    string? Name,
    string? District,
    DateTimeOffset CreatedAt);

public sealed record FavoritesResponse(IReadOnlyList<FavoriteResponse> Items);
