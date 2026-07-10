using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/antiforgery/token", HandleGetAntiforgeryToken)
            .WithName("GetAntiforgeryToken")
            .DisableAntiforgery();

        var group = app.MapGroup("/auth");

        group.MapPost("/register", HandleRegister).WithName("Register").DisableAntiforgery();
        group.MapPost("/login", HandleLogin).WithName("Login").DisableAntiforgery();
    }

    private static IResult HandleGetAntiforgeryToken(IAntiforgery antiforgery, HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { token = tokens.RequestToken });
    }

    private static async Task<IResult> HandleRegister(
        RegisterRequest req,
        UserManager<IdentityUser> userManager)
    {
        var user = new IdentityUser { UserName = req.Email, Email = req.Email };
        var result = await userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors.Select(e => e.Description));

        return Results.Ok(new { message = "User registered successfully." });
    }

    private static async Task<IResult> HandleLogin(
        LoginRequest req,
        UserManager<IdentityUser> userManager,
        TokenService tokenService)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, req.Password))
            return Results.Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new { token = tokenService.CreateToken(user, roles) });
    }
}

record RegisterRequest(string Email, string Password);
record LoginRequest(string Email, string Password);
