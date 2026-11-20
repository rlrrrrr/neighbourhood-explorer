using System.Globalization;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class FakeEmailSender(
    ILogger<FakeEmailSender> logger,
    IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Confirmation email queued for {Email} in {Locale}: {Subject}",
            MaskEmail(message.To),
            message.Locale,
            message.Subject);

        var outboxPath = ResolveOutboxPath(configuration);
        Directory.CreateDirectory(outboxPath);

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.txt");
        var content = $"To: {message.To}\nSubject: {message.Subject}\n\n{message.Body}\n";
        await File.WriteAllTextAsync(Path.Combine(outboxPath, fileName), content, cancellationToken);
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex > 0 ? $"{email[0]}***{email[atIndex..]}" : "***";
    }

    private static string ResolveOutboxPath(IConfiguration configuration)
    {
        var configuredPath = configuration["EMAIL_OUTBOX_PATH"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "outbox"));
    }
}
