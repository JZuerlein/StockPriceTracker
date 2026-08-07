using System.Net;
using Xunit.Abstractions;
using StockPriceTracker.Tests.Integration.AuthenticationHandlers;
using StockPriceTracker.Tests.Integration.DatabaseFixtures;

namespace StockPriceTracker.Tests.Integration.StockEndpointTests;

public abstract class GetStockTestsBase<TFixture> : WebAppTestBase<TFixture>
    where TFixture : WebAppFixtureBase
{
    protected GetStockTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper)
    {
    }

    private static string GetStockUrl(string ticker) => $"/stocks/{ticker}";

    #region JWT Authentication Tests

    [Fact]
    public async Task GetStock_WithJwtAuth_ReturnsStock_WhenTickerExists()
    {
        //Arrange
        var seeded = Stocks[0];
        var client = CreateClient()
            .WithJwtAuth(claims => claims.AsUser("alice"))
            .Build();

        //Act
        var response = await client.GetAsync(GetStockUrl(seeded.Ticker));

        //Assert — any authenticated user may read; no role is required
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stock = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(stock);
        Assert.Equal(seeded.Ticker, stock.Ticker);
        Assert.Equal(seeded.Price, stock.Price);
    }

    [Fact]
    public async Task GetStock_ReturnsStock_WhenTickerCasingDiffers()
    {
        //Arrange
        var seeded = Stocks[0];
        var client = CreateClient()
            .WithJwtAuth(claims => claims.AsUser("alice"))
            .Build();

        //Act — seeded tickers are uppercase; the endpoint uppercases the route value
        var response = await client.GetAsync(GetStockUrl(seeded.Ticker.ToLower()));

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stock = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(stock);
        Assert.Equal(seeded.Ticker, stock.Ticker);
    }

    [Fact]
    public async Task GetStock_ReturnsNotFound_WhenTickerDoesNotExist()
    {
        //Arrange
        var client = CreateClient()
            .WithJwtAuth(claims => claims.AsUser("alice"))
            .Build();

        //Act
        var response = await client.GetAsync(GetStockUrl("NOPE999"));

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Cookie Authentication Tests

    [Fact]
    public async Task GetStock_WithCookieAuth_ReturnsStock_WhenTickerExists()
    {
        //Arrange
        var seeded = Stocks[0];
        var client = CreateClient()
            .WithCookieAuth(claims => claims.AsUser("alice"))
            .Build();

        // No CSRF token: the antiforgery filter only guards the POST endpoint.

        //Act
        var response = await client.GetAsync(GetStockUrl(seeded.Ticker));

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stock = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(stock);
        Assert.Equal(seeded.Ticker, stock.Ticker);
        Assert.Equal(seeded.Price, stock.Price);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetStock_IsUnauthorized_WhenAnonymous()
    {
        //Arrange
        var client = CreateClient().Build(); // no identity supplied

        //Act
        var response = await client.GetAsync(GetStockUrl(Stocks[0].Ticker));

        //Assert — no identity: authentication has nothing to hand authorization
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}

public class GetStockWithSqliteTests : GetStockTestsBase<SqliteFixture>, IClassFixture<SqliteFixture>
{
    public GetStockWithSqliteTests(SqliteFixture fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper)
    {
    }
}

public class GetStockWithPostgreSqlTests : GetStockTestsBase<PostgreSqlFixture>, IClassFixture<PostgreSqlFixture>
{
    public GetStockWithPostgreSqlTests(PostgreSqlFixture fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper)
    {
    }
}
