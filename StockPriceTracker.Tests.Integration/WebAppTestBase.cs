
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPriceTracker.Tests.Integration.AuthenticationHandlers;
using Xunit.Abstractions;

namespace StockPriceTracker.Tests.Integration;

public abstract class WebAppTestBase<TFixture>
    where TFixture : WebAppFixtureBase
{
    protected readonly TFixture Fixture;
    protected readonly ITestOutputHelper Output;

    protected WebAppTestBase(TFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        Output = output;
    }
    
    /// <summary>
    /// Gets the underlying WebApplicationFactory for advanced scenarios.
    /// </summary>
    protected WebApplicationFactory<Program> Factory => Fixture.Factory;

    /// <summary>
    /// Gets the test configuration.
    /// </summary>
    protected IConfiguration? Configuration => Fixture.Configuration;

    protected Stock[] Stocks => Fixture.Stocks;
    
    /// <summary>
    /// Creates an HTTP client builder for configuring authentication.
    /// Use .WithJwtAuth() or .WithCookieAuth() to configure authentication,
    /// then call .Build() to get the HttpClient.
    /// </summary>
    /// <example>
    /// var client = CreateClient()
    ///     .WithJwtAuth(claims => claims.AsAdmin())
    ///     .Build();
    /// </example>
    protected AuthenticatedClientBuilder CreateClient() => Fixture.CreateClient();
    
    /// <summary>
    /// Executes an action against the EF Core context.
    /// </summary>
    protected Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
        => Fixture.ExecuteDbContextAsync(action);

    /// <summary>
    /// Executes an action against the EF Core context and returns a result.
    /// </summary>
    protected Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
        => Fixture.ExecuteDbContextAsync(action);

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    protected T GetService<T>() where T : notnull
        => Fixture.GetService<T>();

    /// <summary>
    /// Creates a new DI scope for manual service resolution.
    /// </summary>
    protected IServiceScope CreateScope()
        => Fixture.CreateScope();
    
    /// <summary>
    /// Writes a message to the test output.
    /// </summary>
    protected void Log(string message) => Output.WriteLine(message);
}