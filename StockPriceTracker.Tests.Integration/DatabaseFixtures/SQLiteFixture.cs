using Microsoft.Extensions.DependencyInjection;

namespace StockPriceTracker.Tests.Integration.DatabaseFixtures;

/// <summary>
/// Test fixture using SQLite file-based database.
/// No container needed - uses a temporary file that is cleaned up after tests.
/// </summary>
public class SqliteFixture : WebAppFixtureBase
{
    // Unique per fixture instance, so parallel test classes never share a file.
    private readonly string _filename = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sqlite");

    // Pooling=False is required for teardown: with pooling on, disposing a connection keeps the
    // file handle open in the pool, so File.Delete would race a live handle and throw.
    protected override string GetConnectionString() => $"Data Source={_filename};Pooling=False";

    protected override void ConfigureDatabaseServices(IServiceCollection services)
    {
        services.AddSqlite();
    }

    // SQLite needs no startup — the file is created on demand when the schema is seeded.
    protected override Task StartDatabaseAsync() => Task.CompletedTask;

    protected override Task StopDatabaseAsync()
    {
        try
        {
            if (File.Exists(_filename))
            {
                File.Delete(_filename);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is harmless; don't fail the run over cleanup.
        }
        return Task.CompletedTask;
    }
}
