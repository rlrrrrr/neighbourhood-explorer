namespace NeighbourhoodExplorer.Api.Http;

public static class ApiProblems
{
    public static IResult DatabaseUnavailable() =>
        Create(
            StatusCodes.Status503ServiceUnavailable,
            "database_unavailable",
            "La base de données n'est pas configurée ou n'est pas joignable.");

    public static IResult NotFound(string code, string detail) =>
        Create(StatusCodes.Status404NotFound, code, detail);

    public static IResult BadRequest(string code, string detail) =>
        Create(StatusCodes.Status400BadRequest, code, detail);

    private static IResult Create(int statusCode, string code, string detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: statusCode switch
            {
                StatusCodes.Status404NotFound => "Ressource introuvable",
                StatusCodes.Status503ServiceUnavailable => "Service indisponible",
                _ => "Requête invalide",
            },
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
