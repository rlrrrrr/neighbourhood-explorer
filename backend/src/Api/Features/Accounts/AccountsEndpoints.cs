using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NeighbourhoodExplorer.Api.Features.Accounts.Contracts;
using Npgsql;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public static class AccountsEndpoints
{
    private const string NeighborhoodTargetType = "neighborhood";

    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/v1/auth").RequireRateLimiting("auth");

        auth.MapGet("/csrf", GetCsrfToken);
        auth.MapPost("/register", RegisterAsync).AddEndpointFilter<AntiforgeryFilter>();
        auth.MapPost("/confirm-email", ConfirmEmailAsync).AddEndpointFilter<AntiforgeryFilter>();
        auth.MapPost("/login", LoginAsync).AddEndpointFilter<AntiforgeryFilter>();
        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryFilter>();

        var me = endpoints.MapGroup("/api/v1/me").RequireAuthorization();

        me.MapGet("/", GetMeAsync);
        me.MapGet("/favorites", ListFavoritesAsync);
        me.MapPut("/favorites/{targetType}/{targetId}", PutFavoriteAsync)
            .AddEndpointFilter<AntiforgeryFilter>();
        me.MapDelete("/favorites/{targetType}/{targetId}", DeleteFavoriteAsync)
            .AddEndpointFilter<AntiforgeryFilter>();

        endpoints.MapAdminEndpoints();

        return endpoints;
    }

    private static IResult GetCsrfToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(new CsrfResponse(tokens.RequestToken ?? string.Empty));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        AccountsDbContext database,
        AccountEmailDelivery emailDelivery,
        AdminAccounts adminAccounts,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var email = AccountInputs.NormalizeEmail(request.Email ?? string.Empty);
        if (!AccountInputs.IsValidEmail(email))
        {
            return AccountErrors.Validation("email_invalid");
        }

        var displayName = AccountInputs.NormalizeDisplayName(request.DisplayName);
        if (!AccountInputs.IsValidDisplayName(displayName))
        {
            return AccountErrors.Validation("display_name_invalid");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return AccountErrors.Validation("password_required");
        }

        var locale = AccountInputs.NormalizeLocale(request.Locale);
        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            if (!await userManager.IsEmailConfirmedAsync(existingUser))
            {
                var existingProfile = await database.UserProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(profile => profile.UserId == existingUser.Id, cancellationToken);

                await emailDelivery.SendConfirmationAsync(
                    new ConfirmationRecipient(
                        existingUser.Email ?? email,
                        existingUser.Id,
                        existingProfile?.DisplayName ?? displayName,
                        existingProfile?.Locale ?? locale),
                    cancellationToken);
            }

            return Results.Accepted(value: new RegisterResponse("confirmation_sent"));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return ValidationFromIdentity(createResult);
        }

        var profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = displayName,
            Locale = locale,
            IsDemo = false,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        database.UserProfiles.Add(profile);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }

        if (adminAccounts.IsAdminEmail(email))
        {
            var roleResult = await userManager.AddToRoleAsync(user, AdminAccounts.RoleName);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The configured administrator account {email} could not receive the admin role.");
            }
        }

        await emailDelivery.SendConfirmationAsync(
            new ConfirmationRecipient(email, user.Id, displayName, locale),
            cancellationToken);

        return Results.Accepted(value: new RegisterResponse("confirmation_sent"));
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Token))
        {
            return AccountErrors.InvalidToken();
        }

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return AccountErrors.InvalidToken();
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? Results.NoContent() : AccountErrors.InvalidToken();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AccountsDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var email = AccountInputs.NormalizeEmail(request.Email ?? string.Empty);
        if (!AccountInputs.IsValidEmail(email) || string.IsNullOrEmpty(request.Password))
        {
            return AccountErrors.InvalidCredentials();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return AccountErrors.InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return AccountErrors.AccountLocked();
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return AccountErrors.EmailNotConfirmed();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return AccountErrors.AccountLocked();
        }

        if (result.IsNotAllowed)
        {
            return AccountErrors.EmailNotConfirmed();
        }

        if (!result.Succeeded)
        {
            return AccountErrors.InvalidCredentials();
        }

        var profile = await FindOrCreateProfileAsync(database, user, timeProvider, cancellationToken);
        var isAdmin = await userManager.IsInRoleAsync(user, AdminAccounts.RoleName);
        return Results.Ok(ToMeResponse(user, profile, isAdmin));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        AccountsDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return AccountErrors.NotAuthenticated();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountErrors.NotAuthenticated();
        }

        var profile = await FindOrCreateProfileAsync(database, user, timeProvider, cancellationToken);
        var isAdmin = await userManager.IsInRoleAsync(user, AdminAccounts.RoleName);
        return Results.Ok(ToMeResponse(user, profile, isAdmin));
    }

    private static async Task<IResult> ListFavoritesAsync(
        HttpContext httpContext,
        AccountsDbContext database,
        ITargetCatalog targetCatalog,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return AccountErrors.NotAuthenticated();
        }

        var favorites = await database.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = favorites.Select(favorite =>
        {
            targetCatalog.TryGet(
                new FavoriteTargetKey(favorite.TargetType, favorite.TargetId),
                out var target);

            return new FavoriteResponse(
                favorite.TargetType,
                favorite.TargetId,
                target?.Name,
                target?.District,
                favorite.CreatedAt);
        }).ToList();

        return Results.Ok(new FavoritesResponse(items));
    }

    private static async Task<IResult> PutFavoriteAsync(
        string targetType,
        string targetId,
        HttpContext httpContext,
        AccountsDbContext database,
        ITargetCatalog targetCatalog,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return AccountErrors.NotAuthenticated();
        }

        if (!IsSupportedTarget(targetType, targetId, targetCatalog))
        {
            return AccountErrors.FavoriteTargetUnknown();
        }

        var alreadySaved = await database.Favorites.AnyAsync(
            favorite => favorite.UserId == userId
                && favorite.TargetType == targetType
                && favorite.TargetId == targetId,
            cancellationToken);

        if (alreadySaved)
        {
            return Results.NoContent();
        }

        database.Favorites.Add(new Favorite
        {
            UserId = userId,
            TargetType = targetType,
            TargetId = targetId,
            CreatedAt = timeProvider.GetUtcNow(),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.NoContent();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteFavoriteAsync(
        string targetType,
        string targetId,
        HttpContext httpContext,
        AccountsDbContext database,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return AccountErrors.NotAuthenticated();
        }

        var favorite = await database.Favorites.FindAsync(
            [userId, targetType, targetId],
            cancellationToken);

        if (favorite is not null)
        {
            database.Favorites.Remove(favorite);
            await database.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static bool IsSupportedTarget(string targetType, string targetId, ITargetCatalog targetCatalog) =>
        string.Equals(targetType, NeighborhoodTargetType, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(targetId)
        && targetCatalog.TryGet(new FavoriteTargetKey(targetType, targetId), out _);

    private static async Task<UserProfile> FindOrCreateProfileAsync(
        AccountsDbContext database,
        ApplicationUser user,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var profile = await database.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);

        if (profile is not null)
        {
            return profile;
        }

        profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = user.Email ?? user.UserName ?? "Neighbourhood Explorer",
            Locale = "fr",
            IsDemo = false,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        database.UserProfiles.Add(profile);
        await database.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static MeResponse ToMeResponse(ApplicationUser user, UserProfile profile, bool isAdmin) =>
        new(user.Id, user.Email ?? string.Empty, profile.DisplayName, profile.Locale, profile.IsDemo, isAdmin);

    private static IResult ValidationFromIdentity(IdentityResult result) =>
        AccountErrors.Validation(result.Errors.Select(ToErrorCode).ToArray());

    private static string ToErrorCode(IdentityError error) =>
        error.Code switch
        {
            "PasswordTooShort" => "password_too_short",
            "PasswordRequiresDigit" => "password_requires_digit",
            "PasswordRequiresLower" => "password_requires_lower",
            "PasswordRequiresUpper" => "password_requires_upper",
            "PasswordRequiresNonAlphanumeric" => "password_requires_non_alphanumeric",
            "PasswordRequiresUniqueChars" => "password_requires_unique_chars",
            "DuplicateEmail" or "DuplicateUserName" => "email_taken",
            "InvalidEmail" or "InvalidUserName" => "email_invalid",
            _ => "validation_failed",
        };
}
