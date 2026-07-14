using System.Net;
using Xunit.Abstractions;
using AutoFixture;
using StockPriceTracker.Tests.Integration.AuthenticationHandlers;
using StockPriceTracker.Tests.Integration.DatabaseFixtures;

namespace StockPriceTracker.Tests.Integration.StockEndpointTests;

public abstract class AddStockTestsBase<TFixture> : WebAppTestBase<TFixture>
    where TFixture : WebAppFixtureBase
{
    private const string AddStockUrl = "/stocks";

    protected AddStockTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper)
    {
    }

    private static StockRequest CreateRequest()
    {
        var fixture = new Fixture();
        return new StockRequest
        (
            Ticker: fixture.Create<string>().ToUpper(),
            Price: fixture.Create<decimal>()
        );
    }
    
    #region JWT Authentication Tests
    
    [Fact]
    public async Task AddStock_WithJwtAuth_CreatesANewStock_WhenDataIsValid()
    {
        //Arrange
        var request = CreateRequest();
        var client = CreateClient()
            .WithJwtAuth(claims => claims.AsAdmin())
            .Build();
        
        //Act
        var response = await client.PostAsJsonAsync(AddStockUrl, request);
        
        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var newStock = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(newStock);
        Assert.Equal(request.Ticker, newStock.Ticker);
        Assert.Equal(request.Price, newStock.Price);
    }
    
    #endregion
    
    #region Cookie Authentication + CSRF Tests

    [Fact]
    public async Task AddStock_WithCookieAuthAndCsrfToken_CreatesANewStock_WhenDataIsValid()
    {
        //Arrange
        var request = CreateRequest();
        var client = CreateClient()
            .WithCookieAuth(claims => claims.AsAdmin())
            .Build();

        // Fetch a real CSRF token; the request carries it in the X-XSRF-TOKEN header.
        await client.WithCsrfTokenAsync();

        //Act
        var response = await client.PostAsJsonAsync(AddStockUrl, request);

        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var newStock = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(newStock);
        Assert.Equal(request.Ticker, newStock.Ticker);
        Assert.Equal(request.Price, newStock.Price);
    }

    [Fact]
    public async Task AddStock_WithCookieAuthButNoCsrfToken_IsRejected()
    {
        //Arrange
        var request = CreateRequest();
        var client = CreateClient()
            .WithCookieAuth(claims => claims.AsAdmin())
            .Build();

        // No CSRF token is fetched — the cookie-auth request should be rejected.

        //Act
        var response = await client.PostAsJsonAsync(AddStockUrl, request);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task AddStock_IsUnauthorized_WhenAnonymous()
    {
        //Arrange
        var request = CreateRequest();
        var client = CreateClient().Build(); // no identity supplied

        //Act
        var response = await client.PostAsJsonAsync(AddStockUrl, request);

        //Assert — no identity: authentication has nothing to hand authorization
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddStock_IsForbidden_WhenAuthenticatedButNotAdmin()
    {
        //Arrange
        var request = CreateRequest();
        var client = CreateClient()
            .WithJwtAuth(claims => claims.AsUser("alice"))
            .Build();

        //Act
        var response = await client.PostAsJsonAsync(AddStockUrl, request);

        //Assert — a known identity that fails the administrator policy
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion
}

public class AddStockWithSqliteTests : AddStockTestsBase<SqliteFixture>, IClassFixture<SqliteFixture>
{
    public AddStockWithSqliteTests(SqliteFixture fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper)
    {
    }
}

public class AddStockWithPostgreSqlTests : AddStockTestsBase<PostgreSqlFixture>, IClassFixture<PostgreSqlFixture>
{
    public AddStockWithPostgreSqlTests(PostgreSqlFixture fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper)
    {
    }
}