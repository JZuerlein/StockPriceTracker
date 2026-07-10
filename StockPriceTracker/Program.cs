var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlite();
builder.Services.AddIdentityAndAuth(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAuthEndpoints();
app.MapStockEndpoints();

app.Run();

// Expose Program to the integration test project
public partial class Program { }
