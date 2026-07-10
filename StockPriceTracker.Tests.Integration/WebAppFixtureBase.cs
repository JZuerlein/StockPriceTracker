using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPriceTracker.Tests.Integration.AuthenticationHandlers;
using Xunit.Abstractions;

namespace StockPriceTracker.Tests.Integration;

public abstract class WebAppFixtureBase : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory;
    public IConfiguration Configuration { get; private set; }
    
    
    public Stock[] Stocks { get; set; } = Array.Empty<Stock>();

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    protected virtual Dictionary<string, string?> GetAdditionalInMemorySettings() => new();

    protected abstract string GetConnectionString();
    
    protected abstract string ConnectionStringConfigKey { get; }
    
    protected abstract void ConfigureDatabaseServices(IServiceCollection services);

    protected abstract Task StartDatabaseAsync();
    
    protected abstract Task StopDatabaseAsync();
    
    /// <summary>
    /// Creates an HTTP client builder for configuring authentication.
    /// </summary>
    public AuthenticatedClientBuilder CreateClient()
    {
        EnsureInitialized();
        return new AuthenticatedClientBuilder(_factory!);
    }
    
    public async Task InitializeAsync()
    {
        await StartDatabaseAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                var inMemorySettings = new Dictionary<string, string?>
                {
                    { ConnectionStringConfigKey, GetConnectionString() }
                };

                foreach (var kvp in GetAdditionalInMemorySettings())
                    inMemorySettings[kvp.Key] = kvp.Value;

                builder.ConfigureAppConfiguration(config =>
                {
                    Configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(inMemorySettings!)
                        .AddJsonFile("appsettings.Testing.json", optional: true, reloadOnChange: true)
                        .Build();

                    config.AddConfiguration(Configuration);
                });

                builder.ConfigureTestServices(services =>
                {
                    ConfigureDatabaseServices(services);
                    services.AddAuthorization();

                    // Replace TimeProvider
                    var existingProvider = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                    if (existingProvider != null)
                    {
                        services.Remove(existingProvider);
                    }

                    services.AddSingleton<TimeProvider>(TimeProvider);
                });
                
                
            });
        
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await PopulateDbAsync(context);
    }
    
    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
        await StopDatabaseAsync();
    }
    
     protected virtual async Task PopulateDbAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        var now = TimeProvider.GetUtcNow().UtcDateTime;
        Stocks = Enumerable.Range(1, 100)
            .Select(i => new Stock
            {
                Ticker = $"TICK{i:D3}",
                Price = Math.Round(10m + i * 1.5m, 4),
                LastUpdated = now
            })
            .ToArray();

        context.Stocks.AddRange(Stocks);
        await context.SaveChangesAsync();
    }
     
    /// <summary>
    /// Gets the underlying WebApplicationFactory for advanced scenarios.
    /// </summary>
    public WebApplicationFactory<Program> Factory
    {
        get
        {
            EnsureInitialized();
            return _factory!;
        }
    }
    
    public async Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
    {
        EnsureInitialized();
        using var scope = _factory!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(context);
    }
    
    public async Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        EnsureInitialized();
        using var scope = _factory!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(context);
    }
    
    public T GetService<T>() where T : notnull
    {
        EnsureInitialized();
        using var scope = _factory!.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
    
    public IServiceScope CreateScope()
    {
        EnsureInitialized();
        return _factory!.Services.CreateScope();
    }
    
    private void EnsureInitialized()
    {
        if (_factory == null)
        {
            throw new InvalidOperationException(
                "WebAppFixture has not been initialized. Ensure InitializeAsync has been called.");
        }
    }
}