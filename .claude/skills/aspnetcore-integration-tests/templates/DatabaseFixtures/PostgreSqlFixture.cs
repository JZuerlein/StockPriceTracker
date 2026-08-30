// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace and copy as-is.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace YourApp.Tests.Integration.DatabaseFixtures;

/// <summary>
/// Real PostgreSQL via Testcontainers. ONE container for the whole test run, started lazily and
/// exactly once, with a uniquely-named database per fixture inside it — pay the container
/// startup cost once, keep test classes isolated from each other's rows.
/// </summary>
public class PostgreSqlFixture : WebAppFixtureBase
{
    private static readonly PostgreSqlContainer SharedContainer = new PostgreSqlBuilder("postgres:15.1").Build();
    private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());

    // Keep the prefix distinctive so a stray container is identifiable at a glance.
    private readonly string _databaseName = $"YourApp_{Guid.NewGuid():N}";

    protected override string GetConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(SharedContainer.GetConnectionString())
        {
            Database = _databaseName
        };

        return builder.ToString();
    }

    protected override void ConfigureDatabaseServices(IServiceCollection services)
    {
        // See the note in SqliteFixture: call your app's own registration extension here
        // instead if you would rather exercise that code path.
        services.AddDbContext<TestDbContext>((sp, options) =>
            options.UseNpgsql(GetConnectionString()));
    }

    protected override Task StartDatabaseAsync() => ContainerStart.Value;

    // The container is shared and static; leave it to Testcontainers' Ryuk to reap at the end
    // of the run rather than stopping it when the first fixture finishes.
    protected override Task StopDatabaseAsync() => Task.CompletedTask;
}
