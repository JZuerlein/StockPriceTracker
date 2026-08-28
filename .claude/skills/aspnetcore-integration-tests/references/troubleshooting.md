# Troubleshooting and anti-patterns

## Anti-patterns to refactor away from

### A `WebApplicationFactory` per test

```csharp
// DON'T
var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(user)));
var client  = factory.CreateClient();
```

Every `WithWebHostBuilder` boots a full application, and the parent factory keeps a reference
to each derived one for the life of the run. With 200 tests you get 200 hosts, none of them
collectable. Symptoms: the suite gets slower as it grows, CI runs out of memory, ports and
file handles leak.

**Fix:** configure auth once per fixture and put the identity on the request
(see `authorization.md`).

### Test identity baked into DI

Any design where "who am I" is a container registration forces a container per identity,
which is the previous anti-pattern wearing a different hat.

**Fix:** stateless handler + per-request header.

### One test body copied per database provider

**Fix:** abstract generic base class over `TFixture`, one thin concrete subclass per provider.

### A container per test class

Starting a Testcontainers container in each fixture instance multiplies startup cost by the
number of test classes.

**Fix:** one `static` container plus a `static Lazy<Task>` start, and a uniquely-named
database per fixture inside it.

### Asserting on `DateTime.UtcNow`

**Fix:** inject `TimeProvider`, replace it in the test host, use `FakeTimeProvider` when the
test cares about time.

### Only testing "anonymous is rejected"

That proves authentication is on. It proves nothing about authorization. Always add the
authenticated-but-wrong-role case and assert **403**, distinct from 401.

---

## Symptom → cause → fix

### `InvalidOperationException: No authenticationScheme was specified, and there was no DefaultChallengeScheme found`

The default scheme cannot challenge. Register `NoOpAuthHandler` and make sure the policy
scheme's `ForwardDefaultSelector` falls back to it when the request carries no `X-Test-Auth`.

### Anonymous requests return 500 instead of 401

Same cause as above.

### Authenticated requests return 401

- The client was built without `.WithJwtAuth`/`.WithCookieAuth`.
- The header name on the client and in `TestAuthPayload.HeaderName` disagree.
- The scheme in the payload is not registered on the host — the forward selector routes to a
  scheme that does not exist.

### Everything returns 403 regardless of role

The `ClaimsBuilder` role claim type does not match what the policy reads. `AsAdmin()` emits
`ClaimTypes.Role`; if the app configures a custom `RoleClaimType`, emit that instead.

### `[Authorize(AuthenticationSchemes = "Bearer")]` endpoints reject valid test identities

A single catch-all test scheme is registered instead of one handler per **real** scheme name.
Register `TestAuthHandler` under `JwtBearerDefaults.AuthenticationScheme` and
`CookieAuthenticationDefaults.AuthenticationScheme`.

### Antiforgery rejects a cookie-authenticated request that did fetch a token

- The client was built without cookie handling, so the antiforgery cookie is not returned.
- The request declared the Bearer scheme; antiforgery checks the cookie scheme specifically.
- The header name from the token endpoint differs from the configured `HeaderName`.

### The CSRF negative test passes but so does everything else

`app.UseAntiforgery()` is missing, or the endpoint has no antiforgery requirement. Confirm the
positive case *needs* the token by deleting the `WithCsrfTokenAsync()` call and watching it
fail.

### `NullReferenceException` on `Configuration` or the factory in a test

The host was never materialized. `WithWebHostBuilder`'s callback is lazy; `InitializeAsync`
must touch `_factory.Services` (`MaterializeHost`) before it returns. The postcondition checks
at the end of `InitializeAsync` exist to catch exactly this.

### The connection string in the host is not the fixture's

In-memory configuration must be added **after** `appsettings.Testing.json` so it wins. Also
confirm `ConnectionStringConfigKey` matches the key the provider registration reads.

### `IOException: The process cannot access the file ... .sqlite` during teardown

Connection pooling is holding the file handle. Use `Pooling=False` in the SQLite connection
string. Teardown also swallows `IOException` — a leftover temp file is not worth failing a run.

### Tests pass alone but fail together

Two test classes share a database. Each fixture instance needs its own database name / file.
If they legitimately share a fixture, they share data: make writes use unique keys and never
mutate a seeded row that another test asserts on.

### Testcontainers: `Docker is either not running or misconfigured`

Docker Desktop is not running, or the runner has no Docker. The SQLite leg still works —
`dotnet test --filter "FullyQualifiedName~Sqlite"` is the fast local loop.

### CI runs out of memory / the runner is killed

Server GC is still on. Set `ServerGarbageCollection`/`ConcurrentGarbageCollection` to `false`
in the test csproj and `DOTNET_gcServer: '0'` in the workflow env, then confirm
`"System.GC.Server": false` in `bin/.../*.runtimeconfig.json`.

### Container-backed tests are flaky only in CI

Integration tests are sharing a job with unit tests, leaving orphaned `dotnet`/`testhost`
processes. Give integration tests their own job.

### The provider matrix filter runs the wrong number of tests

`--filter "FullyQualifiedName~Sqlite"` is a substring match over the fully qualified name,
which includes the method name. Keep the provider fragment unique to class names
(`<Thing>With<Provider>Tests`) and check the reported test count per leg partitions the suite
exactly.
