// TEMPLATE - aspnetcore-integration-tests skill.
// Replace: YourApp.Tests.Integration -> your test namespace, AppDbContext -> your DbContext,
// Stock/StockRequest -> your entities, TestProgram -> your entry point if you use `Program`.

using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace YourApp.Tests.Integration.DatabaseFixtures;

/// <summary>
/// Test fixture using PostgreSQL via Testcontainers.
/// </summary>
public class PostgreSqlFixture : WebAppFixtureBase
{
    private static readonly PostgreSqlContainer SharedContainer = new PostgreSqlBuilder("postgres:15.1").Build();
    private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());

    private readonly string _databaseName = $"StockPriceTracker_{Guid.NewGuid():N}";

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
        services.AddPostgreSql();
    }

    protected override Task StartDatabaseAsync() => ContainerStart.Value;

    protected override Task StopDatabaseAsync() => Task.CompletedTask;
}