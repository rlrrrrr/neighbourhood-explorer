namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class Favorite
{
    public required string UserId { get; init; }

    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
