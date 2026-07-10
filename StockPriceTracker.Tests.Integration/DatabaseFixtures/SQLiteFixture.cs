using Microsoft.Extensions.DependencyInjection;

namespace StockPriceTracker.Tests.Integration.DatabaseFixtures;

/// <summary>
/// Test fixture using SQLite file-based database.
/// No container needed - uses a temporary file that is cleaned up after tests.
/// </summary>
public class SqliteFixture : WebAppFixtureBase
{
    private readonly string _filename = $"{Guid.NewGuid()}.sqlite";

    protected override string ConnectionStringConfigKey => "ConnectionStrings:DefaultConnection";

    protected override string GetConnectionString()
    {
        return $"Data Source={_filename};Pooling=False";
    }

    protected override void ConfigureDatabaseServices(IServiceCollection services)
    {
        services.AddSqlite();
    }

    protected override Task StartDatabaseAsync()
    {
        // Clean up any existing file
        if (File.Exists(_filename))
        {
            File.Delete(_filename);
        }
        return Task.CompletedTask;
    }

    protected override Task StopDatabaseAsync()
    {
        // Clean up the database file
        try
        {
            if (File.Exists(_filename))
            {
                File.Delete(_filename);
            }
        }
        catch (IOException)
        {
            // Ignore cleanup errors
        }
        return Task.CompletedTask;
    }
}
