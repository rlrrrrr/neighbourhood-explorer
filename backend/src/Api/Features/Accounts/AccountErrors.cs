namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed record ApiError(string Code, IReadOnlyList<string> Errors);

public static class AccountErrors
{
    public static IResult Validation(params string[] errors) =>
        Result("validation_failed", StatusCodes.Status400BadRequest, errors);

    public static IResult InvalidToken() =>
        Result("invalid_token", StatusCodes.Status400BadRequest);

    public static IResult InvalidCredentials() =>
        Result("invalid_credentials", StatusCodes.Status401Unauthorized);

    public static IResult EmailNotConfirmed() =>
        Result("email_not_confirmed", StatusCodes.Status403Forbidden);

    public static IResult AccountLocked() =>
        Result("account_locked", StatusCodes.Status423Locked);

    public static IResult NotAuthenticated() =>
        Result("not_authenticated", StatusCodes.Status401Unauthorized);

    public static IResult UserNotFound() =>
        Result("user_not_found", StatusCodes.Status404NotFound);

    public static IResult CannotDeleteSelf() =>
        Result("cannot_delete_self", StatusCodes.Status409Conflict);

    public static IResult FavoriteTargetUnknown() =>
        Result("favorite_target_unknown", StatusCodes.Status400BadRequest);

    public static IResult CsrfFailed() =>
        Result("csrf_failed", StatusCodes.Status400BadRequest);

    public static IResult RateLimited() =>
        Result("rate_limited", StatusCodes.Status429TooManyRequests);

    private static IResult Result(string code, int statusCode, params string[] errors) =>
        Results.Json(new ApiError(code, errors), statusCode: statusCode);
}
