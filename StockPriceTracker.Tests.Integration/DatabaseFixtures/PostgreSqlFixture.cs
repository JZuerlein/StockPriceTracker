using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace StockPriceTracker.Tests.Integration.DatabaseFixtures;

/// <summary>
/// Test fixture using PostgreSQL via Testcontainers.
/// </summary>
public class PostgreSqlFixture : WebAppFixtureBase
{
    private static readonly PostgreSqlContainer SharedContainer = new PostgreSqlBuilder().Build();
    private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());

    private readonly string _databaseName = $"StockPriceTracker_{Guid.NewGuid():N}";

    protected override string ConnectionStringConfigKey => "ConnectionStrings:DefaultConnection";

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