# StockPriceTracker

A small ASP.NET Core (.NET 10) minimal-API demo that tracks stock prices behind
authentication. The application itself is intentionally tiny and was "vibe coded" as
a demo — but its **integration test suite is the point of this repository**. The tests
are written deliberately, as a reusable model for testing minimal-API applications
against real infrastructure. If you're here to learn one thing, read
[`StockPriceTracker.Tests.Integration`](StockPriceTracker.Tests.Integration).

## What the app does

A minimal API with JWT and cookie authentication over ASP.NET Core Identity:

| Endpoint | Method | Auth | Description |
| --- | --- | --- | --- |
| `/auth/register` | POST | Anonymous | Register an Identity user |
| `/auth/login` | POST | Anonymous | Log in, returns a JWT |
| `/antiforgery/token` | GET | Anonymous | Issue an antiforgery (CSRF) token |
| `/stocks/{ticker}` | GET | Authenticated | Look up a stock by ticker |
| `/stocks` | POST | `administrator` role | Add a new stock |

Supporting pieces: EF Core with **both SQLite and PostgreSQL** providers, an injected
`TimeProvider` for deterministic timestamps, JWT issuance via `TokenService`, and role/admin
seeding on startup.

### Project layout

```
StockPriceTracker/                     The application
  Program.cs                           Composition root; exposes `partial class Program` for tests
  Endpoints/                           Auth + Stock minimal-API endpoint groups
  Extensions/ServiceExtensions.cs      AddSqlite / AddPostgreSql / AddIdentityAndAuth
  Data/                                AppDbContext + startup DatabaseInitializer
  Services/TokenService.cs             JWT creation

StockPriceTracker.Tests.Integration/   The main event (see below)
```

## Running it

Requires the **.NET 10 SDK**. The PostgreSQL integration tests also need a running
**Docker** engine (they use [Testcontainers](https://dotnet.testcontainers.org/)).

```bash
# Run the app (SQLite by default)
dotnet run --project StockPriceTracker

# Run the full integration suite (SQLite + PostgreSQL)
dotnet test
```

> The app needs a `Jwt:Key` at startup (see `appsettings.Development.json` / user secrets).
> The tests supply their own key via `appsettings.Testing.json`, so `dotnet test` works
> out of the box.

## The integration tests — and why they're worth copying

These tests boot the **real application** in-memory with
`WebApplicationFactory<Program>` and drive it over HTTP. Several patterns here are
worth lifting into your own projects.

### 1. One test body, run against every database provider

The test logic is written **once** in a generic base class and then executed against
each database provider by declaring a thin concrete subclass per provider:

```csharp
public abstract class AddStockTestsBase<TFixture> : WebAppTestBase<TFixture>
    where TFixture : WebAppFixtureBase
{
    [Fact]
    public async Task AddStock_WithJwtAuth_CreatesANewStock_WhenDataIsValid() { /* ... */ }
}

// Same tests, two real databases — zero duplicated test code:
public class AddStockWithSqliteTests     : AddStockTestsBase<SqliteFixture>,     IClassFixture<SqliteFixture> { }
public class AddStockWithPostgreSqlTests : AddStockTestsBase<PostgreSqlFixture>, IClassFixture<PostgreSqlFixture> { }
```

You get the speed of SQLite during development **and** the fidelity of a real
PostgreSQL server — from the same assertions. Add a provider by adding one fixture and
one one-line subclass.

### 2. A shared PostgreSQL container, isolated per-fixture databases

`PostgreSqlFixture` starts **one** Testcontainers PostgreSQL container for the whole
test run (started exactly once, lazily), then hands each fixture its own uniquely-named
database inside that container:

```csharp
private static readonly PostgreSqlContainer SharedContainer = new PostgreSqlBuilder().Build();
private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());
private readonly string _databaseName = $"StockPriceTracker_{Guid.NewGuid():N}";
```

This is the sweet spot: pay the container startup cost once, but keep test classes
isolated from each other's data. `SqliteFixture` mirrors the same contract with a
throwaway per-fixture `.sqlite` file, so the two providers are interchangeable.

### 3. A fluent authenticated-client builder (JWT *and* cookie auth)

Tests never juggle real passwords or tokens. A test auth handler is swapped into the
DI container and emits whatever claims you ask for, behind a fluent builder:

```csharp
var client = CreateClient()
    .WithJwtAuth(claims => claims.AsAdmin())   // or .WithCookieAuth(...)
    .Build();
```

- `WithJwtAuth` / `WithCookieAuth` select the scheme and replace the real handler with
  `ConfigurableTestAuthHandler`.
- `ClaimsBuilder` (`AsAdmin()`, `AsUser(id)`, `WithRole(...)`, `WithClaim(...)`) makes the
  identity under test explicit and readable.
- Because the handler injects claims directly, you test **authorization** (roles, policies)
  without standing up a login flow.

### 4. First-class CSRF / antiforgery testing

Cookie-authenticated tests exercise the real antiforgery pipeline. Clients are built
with cookie handling enabled, and extension methods make the CSRF dance a one-liner —
including the *negative* case:

```csharp
await client.WithCsrfTokenAsync();   // fetch + attach the token like a browser would
client.WithoutCsrfToken();           // prove protected calls are rejected without it
```

### 5. A deliberately sequenced, self-checking fixture lifecycle

`WebAppFixtureBase.InitializeAsync` spells out its startup as four ordered, named phases
and then **asserts its own postconditions**, so a future refactor that accidentally makes
host construction lazy fails loudly in setup instead of mysteriously later:

```csharp
await StartDatabaseAsync();   // 1. DB is up …
BuildFactory();               // 2. … before the host reads its connection string
var host = MaterializeHost();  // 3. force the host to build now, deterministically
await SeedAsync(host);        // 4. seed known data
```

The base class is heavily commented with the *why* behind each step — it's meant to be
read. It also centralizes the helpers every test needs: `ExecuteDbContextAsync(...)` for
asserting against the database, `GetService<T>()` / `CreateScope()` for reaching into DI,
and a seeded `Stocks` array of known fixtures.

### 6. Deterministic time and generated test data

`TimeProvider` is injected and replaced in the test host, so timestamps are controllable
rather than wall-clock. Request payloads are generated with
[AutoFixture](https://github.com/AutoFixture/AutoFixture), keeping tests focused on
behavior rather than hand-written sample data.

---

### Putting it together

A complete test reads top-to-bottom as *arrange the identity → act over HTTP → assert the
result*, with all the infrastructure hidden behind the base classes:

```csharp
var request = CreateRequest();
var client  = CreateClient().WithJwtAuth(claims => claims.AsAdmin()).Build();

var response  = await client.PostAsJsonAsync("/stocks", request);

Assert.Equal(HttpStatusCode.Created, response.StatusCode);
var created = await response.Content.ReadFromJsonAsync<Stock>();
Assert.Equal(request.Ticker, created!.Ticker);
```

That same test just ran against real PostgreSQL and against SQLite.
