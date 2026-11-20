using Microsoft.AspNetCore.Identity;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012",
        "1234567890123",
        "12345678901234",
        "123456789012345",
        "1234567890123456",
        "password1234",
        "password12345",
        "password123456",
        "passwordpassword",
        "qwertyuiop12",
        "qwertyuiop123",
        "qwerty123456",
        "azerty123456",
        "motdepasse12",
        "motdepasse123",
        "motdepasse1234",
        "iloveyou1234",
        "welcome12345",
        "letmein12345",
        "admin1234567",
        "administrator",
        "changeme1234",
        "football1234",
        "baseball1234",
        "basketball12",
        "sunshine1234",
        "princess1234",
        "dragon123456",
        "monkey123456",
        "master123456",
        "shadow123456",
        "superman1234",
        "batman123456",
        "trustno1234",
        "starwars1234",
        "whatever1234",
        "freedom12345",
        "computer1234",
        "internet1234",
        "samsung12345",
        "google123456",
        "facebook1234",
        "instagram123",
        "linkedin1234",
        "twitter12345",
        "microsoft123",
        "apple1234567",
        "amazon123456",
        "youtube12345",
        "netflix12345",
        "spotify12345",
        "türkiye12345",
        "turkiye12345",
        "istanbul1234",
        "ankara123456",
        "izmir1234567",
        "kadikoy12345",
    };

    public Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user,
        string? password)
    {
        if (password is not null && CommonPasswords.Contains(password))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "password_too_common",
                Description = "password_too_common",
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}
