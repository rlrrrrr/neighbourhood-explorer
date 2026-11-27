namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class UserProfile
{
    public required string UserId { get; init; }

    public required string DisplayName { get; set; }

    public required string Locale { get; set; }

    public bool IsDemo { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? DeletedAt { get; set; }
}
