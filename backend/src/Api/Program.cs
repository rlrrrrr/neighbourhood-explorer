using NeighbourhoodExplorer.Api;
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

builder.Services.AddSingleton(_ => new DatabaseHealthProbe(
    string.IsNullOrWhiteSpace(databaseConnectionString)
        ? null
        : new NpgsqlDataSourceBuilder(databaseConnectionString).Build()));

var app = builder.Build();

app.UseCors("frontend");

app.MapGet("/api/v1/health", async (
    DatabaseHealthProbe databaseProbe,
    IConfiguration configuration,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var database = "unavailable";
    var dataSource = databaseProbe.DataSource;
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

app.Run();

public partial class Program;
