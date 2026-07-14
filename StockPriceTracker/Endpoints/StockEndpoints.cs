using StockPriceTracker.Security;

public static class StockEndpoints
{
    public static void MapStockEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/stocks");

        group.MapGet("/{ticker}", GetStock)
            .RequireAuthorization()
            .WithName("GetStock");

        group.MapPost("", AddStock)
            .RequireAuthorization(policy => policy.RequireRole("administrator"))
            .AddEndpointFilter<CookieAntiforgeryFilter>()
            .WithName("AddStock");
    }

    private static async Task<IResult> GetStock(string ticker, AppDbContext db)
    {
        var stock = await db.Stocks.FindAsync(ticker.ToUpper());
        return stock is null ? Results.NotFound() : Results.Ok(stock);
    }

    private static async Task<IResult> AddStock(StockRequest req, AppDbContext db, TimeProvider timeProvider)
    {
        var ticker = req.Ticker.Trim().ToUpper();
        if (string.IsNullOrEmpty(ticker))
            return Results.BadRequest("Ticker is required.");

        if (await db.Stocks.FindAsync(ticker) is not null)
            return Results.Conflict($"Ticker '{ticker}' already exists.");

        var stock = new Stock { Ticker = ticker, Price = req.Price, LastUpdated = timeProvider.GetUtcNow().UtcDateTime };
        db.Stocks.Add(stock);
        await db.SaveChangesAsync();

        return Results.CreatedAtRoute("GetStock", new { ticker }, stock);
    }
}

public record StockRequest(string Ticker, decimal Price);
