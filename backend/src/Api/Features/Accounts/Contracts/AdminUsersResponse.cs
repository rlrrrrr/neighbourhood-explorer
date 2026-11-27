namespace NeighbourhoodExplorer.Api.Features.Accounts.Contracts;

public sealed record AdminUserResponse(
    string UserId,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    bool EmailConfirmed,
    bool IsDemo,
    bool IsAdmin);

public sealed record AdminUsersResponse(IReadOnlyList<AdminUserResponse> Items);
