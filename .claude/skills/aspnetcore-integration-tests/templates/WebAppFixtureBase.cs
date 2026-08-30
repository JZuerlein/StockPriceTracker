// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace and copy as-is;
// this file names no application type directly (see TestAliases.cs).

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourApp.Tests.Integration.AuthenticationHandlers;

namespace YourApp.Tests.Integration;

/// <summary>
/// Owns one host and one database for the lifetime of a test class. Everything here is
/// application-agnostic: the app's entry point and DbContext arrive as aliases from
/// TestAliases.cs, and anything domain-shaped is left to <see cref="PopulateDbAsync"/> and to
/// whatever your own fixture base adds on top.
/// </summary>
public abstract class WebAppFixtureBase : IAsyncLifetime
{
    private WebApplicationFactory<TestEntryPoint>? _factory;

    public IConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// Clock the host uses. Override with a FakeTimeProvider in a derived fixture when tests
    /// need to control time; the host never sees the wall clock unless you let it.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    protected virtual Dictionary<string, string?> GetAdditionalInMemorySettings() => new();

    protected abstract string GetConnectionString();

    /// <summary>
    /// Configuration key the connection string is written to. Defaults to the conventional
    /// "ConnectionStrings:DefaultConnection"; override if your app reads a different key.
    /// </summary>
    protected virtual string ConnectionStringConfigKey => "ConnectionStrings:DefaultConnection";

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
        _factory = new WebApplicationFactory<TestEntryPoint>()
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
                    // In-memory settings are added last so they win: the per-fixture connection
                    // string must not be silently overridden by a value in appsettings.Testing.json.
                    Configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.Testing.json", optional: true, reloadOnChange: true)
                        .AddInMemoryCollection(inMemorySettings!)
                        .Build();

                    config.AddConfiguration(Configuration);
                });

                builder.ConfigureTestServices(services =>
                {
                    ConfigureDatabaseServices(services);
                    services.AddAuthorization();

                    // Authentication is configured ONCE here, for the whole host — never per test
                    // and never per client. The identity is not baked into the container; it rides
                    // on each request's X-Test-Auth header and is read by the stateless
                    // TestAuthHandler. That is what lets a single WebApplicationFactory serve every
                    // test's identity instead of booting a fresh host per identity.
                    //
                    // A policy scheme is the default. Its forward selector picks the REAL scheme per
                    // request from the header (Cookie or Bearer), so scheme-name lookups stay
                    // faithful — [Authorize(AuthenticationSchemes = "Bearer")], the antiforgery
                    // filter's cookie-scheme check, etc. Requests with no header forward to the
                    // no-op scheme, which yields a clean 401 on challenge for anonymous callers.
                    //
                    // Registering a scheme your app does not use is harmless: nothing forwards to
                    // it. Drop a line only if you want that scheme to be unreachable in tests.
                    services.AddAuthentication(TestAuthSchemes.Selector)
                        .AddPolicyScheme(TestAuthSchemes.Selector, TestAuthSchemes.Selector, options =>
                        {
                            options.ForwardDefaultSelector = context =>
                                TestAuthPayload.ReadScheme(context.Request) ?? NoOpAuthHandler.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions, NoOpAuthHandler>(NoOpAuthHandler.SchemeName, _ => { })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(CookieAuthenticationDefaults.AuthenticationScheme, _ => { })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });

                    // Deterministic time: replace whatever the app registered.
                    var existingProvider = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                    if (existingProvider != null)
                        services.Remove(existingProvider);

                    services.AddSingleton<TimeProvider>(TimeProvider);

                    ConfigureAdditionalServices(services);
                });
            });
    }

    /// <summary>
    /// Hook for replacing anything else the tests must not reach for real — an outbound HTTP
    /// client, a message bus, a payment gateway. Runs last, so it can override the registrations
    /// above. Left empty deliberately: swap infrastructure, not the behaviour under test.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services) { }

    /// <summary>
    /// Forces the <see cref="WebApplicationFactory{TEntryPoint}"/> to build its host now and
    /// returns the root service provider. The WithWebHostBuilder callback (including
    /// GetConnectionString and the assignment to <see cref="Configuration"/>) runs lazily on
    /// first access to <c>Services</c>. Triggering it here — while the database is known to be
    /// running — pins host construction to a deterministic point in the lifecycle instead of
    /// leaving it to whichever test first calls CreateClient(). Building the host does not open
    /// a database connection; that happens during seeding.
    /// </summary>
    private IServiceProvider MaterializeHost() => _factory!.Services;

    private async Task SeedAsync(IServiceProvider host)
    {
        using var scope = host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await PopulateDbAsync(context);
    }

    /// <summary>
    /// Creates the schema and seeds known data. The default creates the schema and stops.
    ///
    /// <para>Override in your own fixture base to seed, and store what you seeded on that class
    /// so tests assert against known rows rather than hardcoded literals. Seed from
    /// <see cref="TimeProvider"/>, never the wall clock.</para>
    ///
    /// <para>If your app uses EF migrations, call <c>MigrateAsync()</c> instead of
    /// <c>EnsureCreatedAsync()</c> — that puts your migrations under test on every run.</para>
    /// </summary>
    protected virtual Task PopulateDbAsync(TestDbContext context)
        => context.Database.EnsureCreatedAsync();

    public async Task DisposeAsync()
    {
        if (_factory != null)
            await _factory.DisposeAsync();

        await StopDatabaseAsync();
    }

    /// <summary>
    /// Gets the underlying WebApplicationFactory for advanced scenarios.
    /// </summary>
    public WebApplicationFactory<TestEntryPoint> Factory
    {
        get
        {
            EnsureInitialized();
            return _factory!;
        }
    }

    public async Task ExecuteDbContextAsync(Func<TestDbContext, Task> action)
    {
        EnsureInitialized();
        using var scope = _factory!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await action(context);
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<TestDbContext, Task<T>> action)
    {
        EnsureInitialized();
        using var scope = _factory!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
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
            throw new InvalidOperationException(
                "WebAppFixture has not been initialized. Ensure InitializeAsync has been called.");
    }
}
