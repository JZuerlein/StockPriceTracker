---
name: aspnetcore-integration-tests
description: Set up and write ASP.NET Core integration tests that boot the real app with WebApplicationFactory and drive it over HTTP against real databases. Use when adding or scaffolding integration tests for an ASP.NET Core / minimal-API project, when tests need multiple database providers (SQLite + PostgreSQL via Testcontainers), when tests need authenticated or role-based requests without a real login flow, when testing antiforgery/CSRF, or when an existing integration suite is slow, flaky, or boots a WebApplicationFactory per test.
---

# ASP.NET Core Integration Tests

Boot the **real application** in-memory with `WebApplicationFactory<T>` and drive it over HTTP.
One test body runs against every database provider. One host serves every identity.

Two directories, and the split is the point:

- **`templates/`** — the infrastructure. Application-agnostic: it names no entity, no DbContext,
  no role, no route. Copy these files, retarget the namespace, and they compile against any
  ASP.NET Core app. Three small files are yours to fill in (`TestAliases.cs`,
  `ClaimsBuilderExtensions.cs`, `TestCsrfSettings.cs`); the rest you never touch again.
- **`examples/`** — the sample app this was extracted from. Read for shape, do not copy.

If you find yourself editing a `templates/` file to name something from your domain, stop:
that thing belongs in your own layer. `examples/ExampleFixtureBase.cs` shows where.

## When to use this

- Scaffolding an integration test project for an ASP.NET Core app (minimal API or MVC).
- Adding endpoint tests that need auth, roles, policies, or CSRF.
- Adding a second database provider to an existing suite.
- Fixing a suite that is slow or flaky (usually: a factory per test, or shared mutable state).

## Non-negotiables

These are the rules that separate a suite that scales from one that rots.

1. **One `WebApplicationFactory` per fixture, never per test.** Every
   `WithWebHostBuilder(...)` boots a full app, and the parent factory *retains every derived
   one* for the life of the run. A suite of N tests must run against 1 host, not N.
2. **Configure authentication once per host, and keep the test identity on the request.**
   The identity rides on a request header and is read by a *stateless* handler. Baking an
   identity into DI forces a host per identity — the same mistake as #1.
3. **Test logic is written once**, in a generic base class, and executed per provider by a
   one-line concrete subclass. Never copy a test body to test a second database.
4. **Forward to the *real* auth scheme names.** Swap credential *validation* only, so
   `[Authorize(AuthenticationSchemes = "Bearer")]` and antiforgery's cookie-scheme check
   stay faithful.
5. **Isolate state per fixture.** A unique database name / file per fixture instance, so
   parallel test classes never see each other's rows.
6. **Order the fixture lifecycle explicitly and assert its postconditions**, so a future
   refactor fails loudly in setup instead of mysteriously later.

## Setup workflow

Work through these in order. Skip a step only if the project already satisfies it.

### 1. Make the app's entry point reachable

`WebApplicationFactory<T>` needs a public entry-point type. Two routes:

**A — use the app's own `Program`.** For top-level statements, add at the bottom of
`Program.cs`:

```csharp
public partial class Program { }
```

The test host then runs your real startup path. Prefer this when startup is test-safe.

**B — a `TestProgram` in the test project** that composes only what tests need (see
`examples/ExampleTestProgram.cs`). Name it `TestProgram`, not `Program`, so it cannot collide
with the app assembly's generated `Program`. Take this route when the real `Program.cs` does
things you do not want on every test run — HTTPS redirection (which turns your requests into
307s), database initialization, background services, outbound handshakes at startup.

Whichever you pick, you name it **once**, in `TestAliases.cs` (step 3). Nothing else in the
infrastructure mentions it. The choice also decides `GenerateProgramFile` in step 2.

### 2. Create the test project

Copy `templates/Tests.Integration.csproj` and merge its property/item groups into your test
project. Package versions there track .NET 10; bump them to match your app. Then:

- Point the `<ProjectReference>` at your app's `.csproj` — the template ships a `..\YourApp\`
  placeholder path.
- **Add the project to your solution**, or your IDE and a bare `dotnet test` at the repo root
  will never see it: `dotnet sln add YourApp.Tests.Integration` (for a `.slnx`, add a
  `<Project Path="..." />` line).
- **Create `appsettings.Testing.json` before your first build.** The csproj has a
  `<Content Include="appsettings.Testing.json">` item, and MSBuild fails the build outright
  if the file is missing (`MSB3030`) — before it ever reaches your code. Put in whatever
  config the host requires at startup (JWT signing key, seed credentials); it is copied to the
  output directory so a bare `dotnet test` works with no user secrets and no environment setup.

Three settings matter beyond the obvious packages:

- `<TargetFramework>` (plus `Nullable` / `ImplicitUsings`) — the template sets these, but
  **delete them if your repo already supplies them from `Directory.Build.props`**. Get this
  backwards in either direction and the project either fails to build or silently disagrees
  with the app about its framework.
- `<GenerateProgramFile>false</GenerateProgramFile>` — **this one depends on your step 1
  choice.** Keep it only if the test project supplies its own `TestProgram.Main`. If you went
  the `public partial class Program { }` route instead, **delete it**: the test SDK's
  generated entry point is then the only one you have, and suppressing it fails the build with
  `CS5001: Program does not contain a static 'Main' method`.
- `<ServerGarbageCollection>false</ServerGarbageCollection>` and
  `<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>` — ASP.NET Core defaults
  to Server GC, one heap per core. On a many-core CI runner, in-process hosts get dozens of
  greedy heaps and peak memory scales with core count: the suite passes locally and OOMs CI.
  Verify it landed in `bin/.../*.runtimeconfig.json` (`"System.GC.Server": false`).

### 3. Point the aliases at your app

Copy `templates/TestAliases.cs` and set two `global using` aliases:

```csharp
global using TestEntryPoint = Program;      // or your TestProgram, from step 1
global using TestDbContext  = AppDbContext; // your EF Core context
```

This is the whole adaptation seam for types. Every other infrastructure file refers to
`TestEntryPoint` and `TestDbContext`, so there is no find-and-replace to do and nothing to
redo the next time you copy these files into a different project. Set it first — nothing else
compiles until the aliases resolve.

### 4. Add the fixture base

Copy `templates/WebAppFixtureBase.cs` unchanged. It owns the whole lifecycle:

```csharp
await StartDatabaseAsync();   // 1. DB is up ...
BuildFactory();               // 2. ... before the host reads its connection string
var host = MaterializeHost(); // 3. force the host to build now, deterministically
await SeedAsync(host);        // 4. seed known data
```

`MaterializeHost` exists because the `WithWebHostBuilder` callback runs **lazily** on first
access to `Services`. Touching it here pins host construction to a known point — while the
database is known to be running — instead of leaving it to whichever test calls
`CreateClient()` first. `InitializeAsync` then asserts its own postconditions, so a refactor
that reintroduces laziness fails in setup with a clear message.

In-memory configuration is added **last** so the per-fixture connection string wins over
anything in `appsettings.Testing.json`.

### 5. Add one fixture per database provider

Copy `templates/DatabaseFixtures/`. Each fixture supplies a connection string, registers the
provider, and starts/stops its database.

- **SQLite** (`SqliteFixture`): a throwaway temp `.sqlite` file per fixture instance. Use
  `Pooling=False` — with pooling on, disposing a connection keeps the file handle in the
  pool and teardown's `File.Delete` races a live handle.
- **PostgreSQL** (`PostgreSqlFixture`): **one** Testcontainers container for the entire run,
  started lazily exactly once via a `static Lazy<Task>`, with a uniquely-named database per
  fixture inside it. This is the sweet spot: pay container startup once, keep classes isolated.

```csharp
private static readonly PostgreSqlContainer SharedContainer = new PostgreSqlBuilder("postgres:15.1").Build();
private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());
private readonly string _databaseName = $"MyApp_{Guid.NewGuid():N}";
```

Both register `AddDbContext<TestDbContext>` directly, so they work against any context. If your
app has its own registration extension and you would rather exercise *that* code path, call it
from `ConfigureDatabaseServices` instead.

Adding a third provider (SQL Server, MySQL) is one new fixture plus one subclass per test class.

### 5b. Add your seeding layer

The generic fixtures create the schema and stop. Seeded data is domain-shaped, so it lives in
**your** code, not in `templates/`. `examples/ExampleFixtureBase.cs` shows the whole pattern in
about 40 lines:

1. an interface naming what your fixtures seed (`ISeededStocks`),
2. one seeding routine shared by every provider,
3. a thin fixture per provider that overrides `PopulateDbAsync` and implements the interface,
4. a test base adding `where TFixture : WebAppFixtureBase, ISeededStocks` so test bodies can
   say `Stocks[0]`.

That last constraint is what keeps `WebAppTestBase` free of your domain while tests still read
naturally. Seed from the fixture's `TimeProvider`, never `DateTime.UtcNow`.

### 6. Add the test auth stack

This is what makes **authorization** testable: it stubs out authentication so any test can
declare a specific identity, then leaves every authorization rule running for real.

Copy the whole of `templates/AuthenticationHandlers/`. Read `references/authorization.md`
before changing any of it — the policy-scheme forwarding is subtle and easy to break in ways
that make authorization tests pass vacuously.

The pieces:

- `TestAuthPayload` — base64 JSON of `{scheme, claims}` carried on the `X-Test-Auth` header.
- `TestAuthHandler` — stateless; reads the identity from the request on every call.
- `NoOpAuthHandler` — fallback for requests with no identity. Returns `NoResult()`, which
  makes it a valid default challenge scheme, so anonymous requests get a clean **401**
  instead of throwing "no authenticationScheme was specified".
- A **policy scheme** whose `ForwardDefaultSelector` routes each request to the real scheme
  it declares (`Bearer` / `Cookies`), or to the no-op scheme.
- `AuthenticatedClientBuilder` + `ClaimsBuilder` — the fluent surface tests actually use.
- `HttpClientCsrfExtensions` — the antiforgery dance, including the negative case.

Exactly two files in that folder are yours to rewrite; the rest copy over unchanged:

- `ClaimsBuilderExtensions.cs` — your roles, policies and claim types (`AsAdmin()` and friends).
- `TestCsrfSettings.cs` — your antiforgery route and header name.

Both compile whatever you put in them, so a stale value fails as a confusing test result rather
than a build error. See "Adapting to your project" below.

### 7. Add the test base class

Copy `templates/WebAppTestBase.cs` unchanged. It is generic over the fixture and forwards the
helpers every test needs regardless of application: `CreateClient()`,
`ExecuteDbContextAsync(...)`, `GetService<T>()`, `CreateScope()`, `Factory`, `Configuration`
and `Log(...)`. Your seeded data reaches tests through the thin subclass from step 5b, not
through this file.

### 8. Wire up CI

Copy `templates/integration-tests.yml` into `.github/workflows/`. Two things it gets right:

- Integration tests run in **their own job**, separate from unit tests. Sharing a job leaves
  orphaned `dotnet`/`testhost` processes behind that destabilise container-backed tests.
- A **matrix over providers**, using `--filter "FullyQualifiedName~<Provider>"` so each
  provider reports independently (`fail-fast: false`). This works because the concrete
  subclasses are named `<Thing>With<Provider>Tests` — keep that convention, and make sure the
  provider fragment cannot accidentally match a test *method* name.

## Adapting to your project

Everything in `templates/` is application-agnostic except **three small files**. Fill those in
and the rest compiles against your app untouched.

| File | What it holds | If you get it wrong |
| --- | --- | --- |
| `TestAliases.cs` | Two `global using` aliases: your entry point and your `DbContext`. | **Build error.** Safe — you cannot miss it. |
| `AuthenticationHandlers/ClaimsBuilderExtensions.cs` | Your roles, policies, claim types. | **Silent.** See below. |
| `AuthenticationHandlers/TestCsrfSettings.cs` | Your antiforgery route and header name. | **Silent.** Wrong endpoint 404s; wrong header attaches the token where nothing reads it, and every cookie-authenticated write returns 400 as though the test were broken. |

Plus two things that are configuration rather than code: `appsettings.Testing.json` (whatever
your host demands at startup) and the `<ProjectReference>` in the csproj.

**The silent ones deserve the attention.** A `ClaimsBuilderExtensions.AsAdmin()` still carrying
the sample's `administrator` role compiles perfectly in an app whose admin role is `Admin` or
`SystemAdministrator`. The identity authenticates, then fails the policy — so the "403 for
non-admins" test passes for entirely the wrong reason, while the *admin* test fails looking
like a product bug. Rewrite that file before you trust any authorization result.

Then write your own layer on top (step 5b): seeding, seeded-data accessors, and any
domain-shaped helper. If you ever find yourself editing a `templates/` file to name one of your
types, that is the signal something belongs in your layer instead.

### Where project vocabulary belongs

Keep `ClaimsBuilder` free of your domain and express identities as extension methods:

```csharp
public static class ClaimsBuilderExtensions
{
    public static ClaimsBuilder AsAdmin(this ClaimsBuilder claims) =>
        claims.AsUser("Administrator").WithRole("administrator");

    public static ClaimsBuilder AsTenantMember(this ClaimsBuilder claims, string userId, string tenantId) =>
        claims.AsUser(userId).WithClaim("tenant_id", tenantId);
}
```

Tests read the same either way — `CreateClient().WithJwtAuth(c => c.AsAdmin())` — but the
generic file stays copyable, and one file holds every assumption about what your roles are
called. A method belongs there if renaming a role or policy in the app should change it.

## Writing a test

Every test class is an abstract generic base plus one thin subclass per provider:

```csharp
public abstract class AddStockTestsBase<TFixture> : StockTestBase<TFixture>
    where TFixture : WebAppFixtureBase, ISeededStocks
{
    protected AddStockTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

    [Fact]
    public async Task AddStock_WithJwtAuth_CreatesANewStock_WhenDataIsValid()
    {
        //Arrange
        var request = CreateRequest();
        var client  = CreateClient().WithJwtAuth(claims => claims.AsAdmin()).Build();

        //Act
        var response = await client.PostAsJsonAsync("/stocks", request);

        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Stock>();
        Assert.NotNull(created);
        Assert.Equal(request.Ticker, created.Ticker);
    }
}

// Same tests, two real databases - zero duplicated test code:
public class AddStockWithSqliteTests : AddStockTestsBase<StocksSqliteFixture>, IClassFixture<StocksSqliteFixture>
{
    public AddStockWithSqliteTests(StocksSqliteFixture f, ITestOutputHelper o) : base(f, o) { }
}

public class AddStockWithPostgreSqlTests : AddStockTestsBase<StocksPostgreSqlFixture>, IClassFixture<StocksPostgreSqlFixture>
{
    public AddStockWithPostgreSqlTests(StocksPostgreSqlFixture f, ITestOutputHelper o) : base(f, o) { }
}
```

`StockTestBase` and the two `Stocks*Fixture` types are the project's own layer from step 5b —
see `examples/ExampleFixtureBase.cs` and `examples/ExampleEndpointTests.cs` for the full pair.

### Conventions

- Put test classes in a folder per endpoint group (`StockEndpointTests/`), one file per
  endpoint, and keep the infrastructure (fixture base, test base, `DatabaseFixtures/`,
  `AuthenticationHandlers/`) at the project root.
- Name tests `Method_Condition_ExpectedResult`, e.g.
  `AddStock_IsForbidden_WhenAuthenticatedButNotAdmin`.
- Mark sections with `//Arrange` / `//Act` / `//Assert`. Group related tests with `#region`
  blocks (JWT, Cookie + CSRF, Authorization).
- Generate request payloads with AutoFixture; hand-write only the values the assertion is
  about.
- Assert against the database with `ExecuteDbContextAsync(...)` when the HTTP response alone
  does not prove the write landed.
- Read seeded data from your fixture's seeded collection (`Stocks[0]`) rather than hardcoding
  values that drift away from the seed.
- Tests that share a fixture share a database. Either only read seeded data, or write rows
  with unique keys — never mutate a seeded row another test asserts on.

### Coverage checklist for every protected endpoint

Do not stop at the happy path. Each endpoint should answer:

| Case | Expected | How |
| --- | --- | --- |
| Valid request, correct role | 2xx | `.WithJwtAuth(c => c.AsAdmin())` |
| No identity at all | **401** | `CreateClient().Build()` |
| Authenticated, wrong role | **403** | `.WithJwtAuth(c => c.AsUser("alice"))` |
| Cookie auth with CSRF token | 2xx | `.WithCookieAuth(...)` then `await client.WithCsrfTokenAsync()` |
| Cookie auth **without** CSRF token | **400** | `.WithCookieAuth(...)`, skip the token |
| Not-found / validation cases | 404 / 400 | as appropriate |
| Duplicate / conflicting write | 409 | post a key the fixture already seeded |

The 401-vs-403 distinction is the point: 401 means authentication had no identity to hand
over; 403 means a *known* identity failed the policy. A suite that only tests "anonymous is
rejected" proves almost nothing about authorization.

## Deterministic time and data

Inject `TimeProvider` in the app and replace it in the test host — the fixture already does
this via its `TimeProvider` property. Use `FakeTimeProvider` from
`Microsoft.Extensions.TimeProvider.Testing` when a test needs to control the clock. Never
assert against `DateTime.UtcNow`.

## Reference material

- `references/authorization.md` — what to assert about roles, policies and claims, and how the
  test auth stack (policy scheme, forwarding, CSRF) keeps those assertions faithful.
- `references/troubleshooting.md` — symptom → cause → fix for the failures this design
  produces, plus the anti-patterns to refactor away from.
