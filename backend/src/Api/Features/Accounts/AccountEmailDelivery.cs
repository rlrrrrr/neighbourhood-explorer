using Microsoft.AspNetCore.Identity;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class AccountEmailDelivery(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IConfiguration configuration)
{
    public async Task SendConfirmationAsync(ConfirmationRecipient recipient, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(recipient.UserId)
            ?? throw new InvalidOperationException("The account to confirm does not exist.");

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var frontendBaseUrl = configuration["FRONTEND_BASE_URL"] ?? "http://localhost:5173";
        var link = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{frontendBaseUrl.TrimEnd('/')}/confirm-email?userId={Uri.EscapeDataString(recipient.UserId)}&token={Uri.EscapeDataString(token)}");

        await emailSender.SendAsync(
            AccountEmailComposer.ComposeConfirmation(recipient.Email, recipient.DisplayName, recipient.Locale, link),
            cancellationToken);
    }
}
