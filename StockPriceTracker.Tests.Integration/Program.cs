using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Minimal service registration for integration tests.
// JWT bearer auth is intentionally omitted — the test infrastructure
// replaces authentication entirely via ConfigurableTestAuthHandler.
builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new TokenService("integration-test-jwt-secret-key-min-32-chars!!", "StockPriceTracker"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAuthEndpoints();
app.MapStockEndpoints();

app.Run();
