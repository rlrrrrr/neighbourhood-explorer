using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NeighbourhoodExplorer.Api.Features.Accounts.Contracts;
using Testcontainers.PostgreSql;

namespace NeighbourhoodExplorer.Api.Tests;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly string? providedConnectionString =
        Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION_STRING");
    private readonly PostgreSqlContainer? container =
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION_STRING"))
            ? new PostgreSqlBuilder("postgres:16-alpine").Build()
            : null;

    public string ConnectionString =>
        providedConnectionString ?? container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (container is not null)
        {
            await container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}

public sealed class AccountsEndpointTests(PostgresContainerFixture database) : IClassFixture<PostgresContainerFixture>
{
    private const string ValidPassword = "une-phrase-de-passe-longue-42";

    [Fact]
    public async Task Registration_confirmation_login_and_favorites_work_end_to_end()
    {
        using var outbox = new TemporaryDirectory();
        using var factory = CreateFactory(database.ConnectionString, outbox.Path);
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        await RegisterAsync(client, email, "Camille", "fr");
        var confirmation = await ReadLatestConfirmationLinkAsync(outbox.Path);
        await ConfirmAsync(client, confirmation);

        var loginResponse = await PostAsync(client, "/api/v1/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        await EnsureCsrfTokenAsync(client);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me");
        Assert.NotNull(me);
        Assert.Equal(email, me.Email);
        Assert.Equal("Camille", me.DisplayName);

        var emptyFavorites = await client.GetFromJsonAsync<FavoritesResponse>("/api/v1/me/favorites");
        Assert.NotNull(emptyFavorites);
        Assert.Empty(emptyFavorites.Items);

        var putResponse = await PutAsync(client, "/api/v1/me/favorites/neighborhood/40512");
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);
        var secondPutResponse = await PutAsync(client, "/api/v1/me/favorites/neighborhood/40512");
        Assert.Equal(HttpStatusCode.NoContent, secondPutResponse.StatusCode);

        var favorites = await client.GetFromJsonAsync<FavoritesResponse>("/api/v1/me/favorites");
        Assert.NotNull(favorites);
        var favorite = Assert.Single(favorites.Items);
        Assert.Equal("Caferağa", favorite.Name);

        var deleteResponse = await DeleteAsync(client, "/api/v1/me/favorites/neighborhood/40512");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var afterDelete = await client.GetFromJsonAsync<FavoritesResponse>("/api/v1/me/favorites");
        Assert.NotNull(afterDelete);
        Assert.Empty(afterDelete.Items);
    }

    [Fact]
    public async Task Registration_does_not_reveal_whether_an_address_exists()
    {
        using var outbox = new TemporaryDirectory();
        using var factory = CreateFactory(database.ConnectionString, outbox.Path);
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var first = await RegisterAsync(client, email, "Camille", "fr");
        var second = await RegisterAsync(client, email, "Autre nom", "en");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
    }

    [Fact]
    public async Task Write_without_csrf_token_is_rejected()
    {
        using var outbox = new TemporaryDirectory();
        using var factory = CreateFactory(database.ConnectionString, outbox.Path);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = UniqueEmail(),
            displayName = "Camille",
            password = ValidPassword,
            locale = "fr",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("csrf_failed", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_favorite_target_is_refused()
    {
        using var outbox = new TemporaryDirectory();
        using var factory = CreateFactory(database.ConnectionString, outbox.Path);
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        await RegisterAsync(client, email, "Camille", "fr");
        await ConfirmAsync(client, await ReadLatestConfirmationLinkAsync(outbox.Path));
        await PostAsync(client, "/api/v1/auth/login", new { email, password = ValidPassword });
        await EnsureCsrfTokenAsync(client);

        using var response = await PutAsync(client, "/api/v1/me/favorites/neighborhood/99999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("favorite_target_unknown", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account()
    {
        using var outbox = new TemporaryDirectory();
        using var factory = CreateFactory(database.ConnectionString, outbox.Path);
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        await RegisterAsync(client, email, "Camille", "fr");
        await ConfirmAsync(client, await ReadLatestConfirmationLinkAsync(outbox.Path));

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var failed = await PostAsync(client, "/api/v1/auth/login", new
            {
                email,
                password = "wrong-password-42",
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using var lockingAttempt = await PostAsync(client, "/api/v1/auth/login", new
        {
            email,
            password = "wrong-password-42",
        });
        Assert.Equal((HttpStatusCode)423, lockingAttempt.StatusCode);

        using var locked = await PostAsync(client, "/api/v1/auth/login", new
        {
            email,
            password = ValidPassword,
        });

        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, string outboxPath) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("DATABASE_CONNECTION_STRING", connectionString)
            .UseSetting("EMAIL_OUTBOX_PATH", outboxPath)
            .UseSetting("FRONTEND_BASE_URL", "http://localhost:5173"));

    private static string UniqueEmail() => $"camille+{Guid.NewGuid():N}@example.test";

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string displayName,
        string locale)
    {
        await EnsureCsrfTokenAsync(client);
        return await PostAsync(client, "/api/v1/auth/register", new
        {
            email,
            displayName,
            password = ValidPassword,
            locale,
        });
    }

    private static async Task ConfirmAsync(HttpClient client, Uri confirmationLink)
    {
        var query = confirmationLink.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
        var userId = query["userId"];
        var token = query["token"];

        using var response = await PostAsync(client, "/api/v1/auth/confirm-email", new { userId, token });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<Uri> ReadLatestConfirmationLinkAsync(string outboxPath)
    {
        var latestFile = new DirectoryInfo(outboxPath)
            .GetFiles("*.txt")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .First();

        var content = await File.ReadAllTextAsync(latestFile.FullName);
        var link = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First(line => line.StartsWith("http", StringComparison.Ordinal));

        return new Uri(link);
    }

    private static async Task EnsureCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        Assert.NotNull(response);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response.Token);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(path, body);

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string path) =>
        client.PutAsync(path, content: null);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path) =>
        client.DeleteAsync(path);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"neighbourhood-email-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
