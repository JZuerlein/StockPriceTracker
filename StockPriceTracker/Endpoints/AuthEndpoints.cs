using Microsoft.AspNetCore.Identity;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", HandleRegister).WithName("Register");
        group.MapPost("/login", HandleLogin).WithName("Login");
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
