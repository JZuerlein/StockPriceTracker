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
        // Each phase depends on the previous one having completed. Expressing that as four
        // named, ordered statements keeps the sequential dependency visible: the database is
        // running before the host reads its connection string, and the host is built before
        // we seed. See MaterializeHost for why the build is forced explicitly rather than
        // left to the first test that touches the factory.
        await StartDatabaseAsync();
        BuildFactory();
        var host = MaterializeHost();
        await SeedAsync(host);

        // Postcondition: the fixture must be fully materialized once InitializeAsync returns.
        // If a future refactor moves the forcing out of MaterializeHost (e.g. by making seeding
        // lazy), these fields would silently drift to null until the first test touched the
        // factory. Assert them here so that regression fails loudly, in setup, with a clear cause.
        if (_factory is null)
            throw new InvalidOperationException(
                "WebAppFixture initialization completed without building the WebApplicationFactory.");
        if (Configuration is null)
            throw new InvalidOperationException(
                "WebAppFixture initialization completed without materializing the host configuration. " +
                "Ensure MaterializeHost forces the host to build before InitializeAsync returns.");
    }

    private void BuildFactory()
    {
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
    }

    /// <summary>
    /// Forces the <see cref="WebApplicationFactory{TEntryPoint}"/> to build its host now and
    /// returns the root service provider. The WithWebHostBuilder callback (including
    /// GetConnectionString and the assignment to <see cref="Configuration"/>) runs lazily on
    /// first access to <c>Services</c>. Triggering it here — while the database is known to be
    /// running — pins host construction to a deterministic point in the lifecycle instead of
    /// leaving it to whichever test first calls CreateClient(). Building the host does not open
    /// a database connection; that happens during seeding.
    /// </summary>
    private IServiceProvider MaterializeHost() => _factory.Services;

    private async Task SeedAsync(IServiceProvider host)
    {
        using var scope = host.CreateScope();
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