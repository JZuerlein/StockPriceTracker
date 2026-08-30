// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace and copy as-is.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace YourApp.Tests.Integration.DatabaseFixtures;

/// <summary>
/// SQLite on a throwaway file. No container, so this is the fast local loop —
/// `dotnet test --filter "FullyQualifiedName~Sqlite"` runs the whole suite without Docker.
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
        // Re-registering DbContextOptions replaces whatever the app registered; the last
        // registration wins. If your app exposes its own registration extension and you would
        // rather exercise that, call it here instead — just make sure it reads the connection
        // string from configuration, which the fixture has already set.
        services.AddDbContext<TestDbContext>((sp, options) =>
            options.UseSqlite(GetConnectionString()));
    }

    // SQLite needs no startup — the file is created on demand when the schema is seeded.
    protected override Task StartDatabaseAsync() => Task.CompletedTask;

    protected override Task StopDatabaseAsync()
    {
        try
        {
            if (File.Exists(_filename))
                File.Delete(_filename);
        }
        catch (IOException)
        {
            // A leftover temp file is harmless; don't fail the run over cleanup.
        }

        return Task.CompletedTask;
    }
}
