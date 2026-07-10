using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace StockPriceTracker.Tests.Integration.DatabaseFixtures;

/// <summary>
/// Test fixture using PostgreSQL via Testcontainers.
/// </summary>
public class PostgreSqlFixture : WebAppFixtureBase
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder().Build();

    protected override string ConnectionStringConfigKey => "ConnectionStrings:DefaultConnection";

    protected override string GetConnectionString()
    {
        return _dbContainer.GetConnectionString();
    }

    protected override void ConfigureDatabaseServices(IServiceCollection services)
    {
        services.AddPostgreSql();
    }

    protected override async Task StartDatabaseAsync()
    {
        await _dbContainer.StartAsync();
    }

    protected override async Task StopDatabaseAsync()
    {
        await _dbContainer.StopAsync();
    }
}