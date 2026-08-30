// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace and copy as-is;
// this file names no application type directly (see TestAliases.cs).

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourApp.Tests.Integration.AuthenticationHandlers;
using Xunit.Abstractions;

namespace YourApp.Tests.Integration;

/// <summary>
/// Base for test classes. Generic over the fixture so one test body runs against every database
/// provider; it exposes only what every test needs, regardless of application.
///
/// <para>Domain conveniences — the seeded rows a suite asserts against — belong on your own
/// intermediate base class, not here. See examples/ExampleFixtureBase.cs.</para>
/// </summary>
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
    protected WebApplicationFactory<TestEntryPoint> Factory => Fixture.Factory;

    /// <summary>
    /// Gets the test configuration.
    /// </summary>
    protected IConfiguration? Configuration => Fixture.Configuration;

    /// <summary>
    /// Creates an HTTP client builder for configuring authentication.
    /// Use .WithJwtAuth() or .WithCookieAuth() to configure authentication,
    /// then call .Build() to get the HttpClient.
    /// </summary>
    /// <example>
    /// var client = CreateClient()
    ///     .WithJwtAuth(claims => claims.WithRole("administrator"))
    ///     .Build();
    /// </example>
    protected AuthenticatedClientBuilder CreateClient() => Fixture.CreateClient();

    /// <summary>
    /// Executes an action against the EF Core context.
    /// </summary>
    protected Task ExecuteDbContextAsync(Func<TestDbContext, Task> action)
        => Fixture.ExecuteDbContextAsync(action);

    /// <summary>
    /// Executes an action against the EF Core context and returns a result.
    /// </summary>
    protected Task<T> ExecuteDbContextAsync<T>(Func<TestDbContext, Task<T>> action)
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
