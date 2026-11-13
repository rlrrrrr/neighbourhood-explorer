namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed record EmailMessage(string To, string DisplayName, string Locale, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
