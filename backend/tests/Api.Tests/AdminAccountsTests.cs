using Microsoft.Extensions.Configuration;
using NeighbourhoodExplorer.Api.Features.Accounts;

namespace NeighbourhoodExplorer.Api.Tests;

public sealed class AdminAccountsTests
{
    [Fact]
    public void Parses_configured_emails_without_duplicates_or_blank_entries()
    {
        var emails = AdminAccounts.Parse(" admin@example.test , ,Admin@Example.Test, second@example.test ");

        Assert.Equal(2, emails.Count);
    }

    [Fact]
    public void Recognizes_a_configured_email_ignoring_case_and_surrounding_spaces()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = "admin@example.test",
            })
            .Build();

        var accounts = new AdminAccounts(configuration);

        Assert.True(accounts.IsAdminEmail(" Admin@Example.Test "));
        Assert.False(accounts.IsAdminEmail("member@example.test"));
        Assert.False(accounts.IsAdminEmail(null));
    }

    [Fact]
    public void Recognizes_nobody_when_the_setting_is_empty()
    {
        var configuration = new ConfigurationBuilder().Build();
        var accounts = new AdminAccounts(configuration);

        Assert.False(accounts.IsAdminEmail("admin@example.test"));
    }
}
