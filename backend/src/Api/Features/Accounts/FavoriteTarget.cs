namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed record FavoriteTarget(string TargetType, string TargetId, string Name, string District);

public sealed record FavoriteTargetKey(string TargetType, string TargetId);
