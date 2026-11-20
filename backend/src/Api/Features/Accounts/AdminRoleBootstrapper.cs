using Microsoft.AspNetCore.Identity;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public static class AdminRoleBootstrapper
{
    public static async Task ApplyAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AdminAccounts.RoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminAccounts.RoleName));
        }

        foreach (var email in AdminAccounts.Parse(configuration["ADMIN_EMAILS"]))
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && !await userManager.IsInRoleAsync(user, AdminAccounts.RoleName))
            {
                await userManager.AddToRoleAsync(user, AdminAccounts.RoleName);
            }
        }
    }
}
