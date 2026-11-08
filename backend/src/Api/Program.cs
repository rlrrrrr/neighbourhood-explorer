using Microsoft.AspNetCore.Diagnostics;
using NeighbourhoodExplorer.Api;
using NeighbourhoodExplorer.Api.Features.Geography;
using NeighbourhoodExplorer.Api.Features.Metrics;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var databaseConnectionString = builder.Configuration["DATABASE_CONNECTION_STRING"];

builder.Services.AddSingleton(_ => new DatabaseConnection(
    string.IsNullOrWhiteSpace(databaseConnectionString)
        ? null
        : new NpgsqlDataSourceBuilder(databaseConnectionString).Build()));

var app = builder.Build();

app.UseCors("frontend");

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var databaseUnavailable = exception is TimeoutException
        || exception is NpgsqlException { IsTransient: true };

    await Results.Problem(
        statusCode: databaseUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError,
        title: databaseUnavailable ? "Service indisponible" : "Erreur interne",
        detail: databaseUnavailable
            ? "La base de données n'est pas joignable."
            : "Une erreur interne est survenue.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = databaseUnavailable ? "database_unavailable" : "internal_error",
        }).ExecuteAsync(context);
}));

app.MapGet("/api/v1/health", async (
    DatabaseConnection databaseConnection,
    IConfiguration configuration,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var database = "unavailable";
    var dataSource = databaseConnection.DataSource;
    if (dataSource is not null)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            database = "ok";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database readiness probe failed");
        }
    }

    var aiEnabled = string.Equals(configuration["AI_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
    var ai = aiEnabled ? "unavailable" : "disabled";
    var configuredDataVersion = configuration["DATA_VERSION"];
    var dataVersion = string.IsNullOrWhiteSpace(configuredDataVersion) ? null : configuredDataVersion;

    return Results.Ok(new HealthResponse(database, dataVersion, ai));
});

app.MapMetricsEndpoints();
app.MapGeographyEndpoints();

app.Run();

public partial class Program;
