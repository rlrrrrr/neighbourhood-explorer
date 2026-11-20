using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NeighbourhoodExplorer.Api.Features.Accounts.Contracts;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class AdminEndpointsLog;

public static class AdminEndpoints
{
    private const int DefaultLimit = 100;
    private const int MaximumLimit = 200;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints
            .MapGroup("/api/v1/admin")
            .RequireAuthorization(policy => policy.RequireRole(AdminAccounts.RoleName));

        admin.MapGet("/users", ListUsersAsync);
        admin.MapDelete("/users/{userId}", DeleteUserAsync)
            .AddEndpointFilter<AntiforgeryFilter>();

        return endpoints;
    }

    private static async Task<IResult> ListUsersAsync(
        AccountsDbContext database,
        int? limit,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaximumLimit);

        var adminRoleId = await database.Roles
            .AsNoTracking()
            .Where(role => role.Name == AdminAccounts.RoleName)
            .Select(role => role.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var adminUserIds = adminRoleId is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : await database.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.RoleId == adminRoleId)
                .Select(userRole => userRole.UserId)
                .ToHashSetAsync(cancellationToken);

        var rows = await (
            from user in database.Users.AsNoTracking()
            join profile in database.UserProfiles.AsNoTracking()
                on user.Id equals profile.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            orderby (profile == null ? DateTimeOffset.MinValue : profile.CreatedAt) descending, user.Email
            select new { User = user, Profile = profile })
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AdminUserResponse(
                row.User.Id,
                row.User.Email ?? string.Empty,
                row.Profile?.DisplayName ?? row.User.Email ?? string.Empty,
                row.Profile?.CreatedAt ?? DateTimeOffset.MinValue,
                row.User.EmailConfirmed,
                row.Profile?.IsDemo ?? false,
                adminUserIds.Contains(row.User.Id)))
            .ToList();

        return Results.Ok(new AdminUsersResponse(items));
    }

    private static async Task<IResult> DeleteUserAsync(
        string userId,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminEndpointsLog> logger,
        CancellationToken cancellationToken)
    {
        var actingUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actingUserId is null)
        {
            return AccountErrors.NotAuthenticated();
        }

        if (string.Equals(actingUserId, userId, StringComparison.Ordinal))
        {
            return AccountErrors.CannotDeleteSelf();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountErrors.UserNotFound();
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return AccountErrors.Validation(result.Errors.Select(error => error.Code).ToArray());
        }

        logger.LogInformation(
            "Administrator {AdminUserId} removed account {RemovedUserId}",
            actingUserId,
            userId);

        return Results.NoContent();
    }
}
