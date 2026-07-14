using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;
using StockPriceTracker.Tests.Integration.DatabaseFixtures;

namespace StockPriceTracker.Tests.Integration.StockEndpointTests;

/// <summary>
/// A SQLite fixture with the clock frozen to a fixed instant. Because the base exposes
/// TimeProvider as an init property and swaps it into DI, injecting a FakeTimeProvider here
/// makes every timestamp the app produces deterministic.
/// </summary>
public sealed class FrozenClockSqliteFixture : SqliteFixture
{
    public static readonly DateTimeOffset FrozenAt = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public FrozenClockSqliteFixture()
    {
        TimeProvider = new FakeTimeProvider(FrozenAt);
    }
}

public class AddStockClockTests : WebAppTestBase<FrozenClockSqliteFixture>, IClassFixture<FrozenClockSqliteFixture>
{
    public AddStockClockTests(FrozenClockSqliteFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    [Fact]
    public async Task AddStock_StampsLastUpdated_FromTheInjectedClock()
    {
        //Arrange
        var client = CreateClient()
            .WithJwtAuth(claims => claims.AsAdmin())
            .Build();

        //Act
        var response = await client.PostAsJsonAsync("/stocks", new StockRequest("CLOCK", 42m));

        //Assert — the endpoint stamped LastUpdated from the frozen clock, not the wall clock
        response.EnsureSuccessStatusCode();
        var stock = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(stock);
        Assert.Equal(FrozenClockSqliteFixture.FrozenAt.UtcDateTime, stock.LastUpdated);
    }

    [Fact]
    public void SeedData_IsStamped_FromTheInjectedClock()
    {
        //Assert — the seed also read the injected clock, so its rows share the frozen instant
        Assert.All(Stocks, s => Assert.Equal(FrozenClockSqliteFixture.FrozenAt.UtcDateTime, s.LastUpdated));
    }
}
