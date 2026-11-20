namespace NeighbourhoodExplorer.Api.Features.Accounts;

public static class AccountEmailComposer
{
    private static readonly IReadOnlyDictionary<string, ConfirmationText> ConfirmationTexts =
        new Dictionary<string, ConfirmationText>(StringComparer.Ordinal)
        {
            ["fr"] = new(
                "Confirmez votre adresse e-mail",
                "Bonjour {displayName},\n\nPour terminer la création de votre compte Neighbourhood Explorer, ouvrez ce lien dans les 24 heures :\n{link}\n\nSi vous n'êtes pas à l'origine de cette demande, ignorez ce message."),
            ["en"] = new(
                "Confirm your email address",
                "Hello {displayName},\n\nTo finish creating your Neighbourhood Explorer account, open this link within 24 hours:\n{link}\n\nIf you did not make this request, ignore this message."),
            ["tr"] = new(
                "E-posta adresinizi doğrulayın",
                "Merhaba {displayName},\n\nNeighbourhood Explorer hesabınızı oluşturmayı tamamlamak için bu bağlantıyı 24 saat içinde açın:\n{link}\n\nBu isteği siz yapmadıysanız bu iletiyi yok sayın."),
        };

    public static EmailMessage ComposeConfirmation(
        string email,
        string displayName,
        string locale,
        string link)
    {
        var text = ConfirmationTexts.GetValueOrDefault(locale) ?? ConfirmationTexts["en"];
        return new EmailMessage(
            email,
            displayName,
            locale,
            text.Subject,
            text.Body.Replace("{displayName}", displayName, StringComparison.Ordinal).Replace("{link}", link, StringComparison.Ordinal));
    }

    private sealed record ConfirmationText(string Subject, string Body);
}
