// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace, and see
// "Adapting to your project" in SKILL.md for what else is project-specific in this file.

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
        services.AddPostgreSql();
    }

    protected override Task StartDatabaseAsync() => ContainerStart.Value;

    protected override Task StopDatabaseAsync() => Task.CompletedTask;
}