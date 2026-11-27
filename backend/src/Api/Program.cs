using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using NeighbourhoodExplorer.Api;
using NeighbourhoodExplorer.Api.Features.Accounts;
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
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/geo+json"]);
});

var databaseConnectionString = builder.Configuration["DATABASE_CONNECTION_STRING"];
if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    databaseConnectionString = null;
}

builder.Services.AddSingleton(_ => new DatabaseConnection(
    databaseConnectionString is null
        ? null
        : new NpgsqlDataSourceBuilder(databaseConnectionString).Build()));

if (databaseConnectionString is not null)
{
    builder.Services.AddDbContext<AccountsDbContext>(options =>
        options.UseNpgsql(databaseConnectionString));

    builder.Services
        .AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AccountsDbContext>()
        .AddDefaultTokenProviders()
        .AddPasswordValidator<CommonPasswordValidator>();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "ne.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiError("not_authenticated", []));
        };

        options.Events.OnRedirectToAccessDenied = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiError("not_authenticated", []));
        };
    });

    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.Cookie.Name = "ne.csrf";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.Always;
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ApiError("rate_limited", []),
                cancellationToken);
        };
    });

    builder.Services.AddAuthorization();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<AdminAccounts>();
    builder.Services.AddScoped<AccountEmailDelivery>();
    builder.Services.AddScoped<IEmailSender, FakeEmailSender>();
    builder.Services.AddSingleton<ITargetCatalog, FixtureTargetCatalog>();
    builder.Services.AddScoped<AntiforgeryFilter>();
}

var app = builder.Build();

app.UseResponseCompression();

if (databaseConnectionString is not null)
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<AccountsDbContext>()
        .Database.MigrateAsync();

    await AdminRoleBootstrapper.ApplyAsync(app.Services, builder.Configuration);
}

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

if (databaseConnectionString is not null)
{
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
}

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

if (databaseConnectionString is not null)
{
    app.MapAccountsEndpoints();
}

app.Run();

public partial class Program;
