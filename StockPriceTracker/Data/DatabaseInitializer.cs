using Microsoft.AspNetCore.Identity;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedRolesAsync(scope.ServiceProvider);
        await SeedAdminUserAsync(scope.ServiceProvider, app.Configuration, app.Logger);
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("administrator"))
            await roleManager.CreateAsync(new IdentityRole("administrator"));
    }

    private static async Task SeedAdminUserAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, password);

        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "administrator");
        else
            logger.LogError("Failed to seed admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
