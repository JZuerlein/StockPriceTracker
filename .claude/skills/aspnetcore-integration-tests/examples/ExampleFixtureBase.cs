// EXAMPLE - aspnetcore-integration-tests skill. Not a template: this is the sample app's own
// code, showing the shape of the layer YOU write on top of the generic infrastructure.
//
// Nothing in templates/ knows what a Stock is. Seeded data, the entities you assert against,
// and any domain-shaped helper live here, in your project. The pieces:
//
//   1. an interface naming what your fixtures seed,
//   2. one seeding routine, shared by every provider,
//   3. a thin fixture per provider,
//   4. a test base that surfaces the seeded data to tests.
//
// Roughly 40 lines, written once per project.

using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using YourApp.Tests.Integration.DatabaseFixtures;

namespace YourApp.Tests.Integration;

// 1. What every fixture in this suite guarantees it has seeded. Tests assert against these
//    rows instead of hardcoding literals that drift away from the seed.
public interface ISeededStocks
{
    Stock[] Stocks { get; }
}

// 2. Seeding lives in one place so adding a third provider never duplicates it. Seed from the
//    fixture's TimeProvider, never DateTime.UtcNow, or timestamps are not reproducible.
public static class StockSeed
{
    public static async Task<Stock[]> PopulateAsync(TestDbContext context, TimeProvider timeProvider)
    {
        await context.Database.EnsureCreatedAsync();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stocks = Enumerable.Range(1, 100)
            .Select(i => new Stock
            {
                Ticker = $"TICK{i:D3}",
                Price = Math.Round(10m + i * 1.5m, 4),
                LastUpdated = now
            })
            .ToArray();

        context.Stocks.AddRange(stocks);
        await context.SaveChangesAsync();

        return stocks;
    }
}

// 3. One per provider. Everything about running the database is inherited; all these add is
//    "and seed my domain into it".
public class StocksSqliteFixture : SqliteFixture, ISeededStocks
{
    public Stock[] Stocks { get; private set; } = Array.Empty<Stock>();

    protected override async Task PopulateDbAsync(TestDbContext context)
        => Stocks = await StockSeed.PopulateAsync(context, TimeProvider);
}

public class StocksPostgreSqlFixture : PostgreSqlFixture, ISeededStocks
{
    public Stock[] Stocks { get; private set; } = Array.Empty<Stock>();

    protected override async Task PopulateDbAsync(TestDbContext context)
        => Stocks = await StockSeed.PopulateAsync(context, TimeProvider);
}

// 4. Your test base. The extra constraint is what lets test bodies say `Stocks[0]` while the
//    generic WebAppTestBase stays free of your domain.
public abstract class StockTestBase<TFixture> : WebAppTestBase<TFixture>
    where TFixture : WebAppFixtureBase, ISeededStocks
{
    protected StockTestBase(TFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected Stock[] Stocks => Fixture.Stocks;
}
