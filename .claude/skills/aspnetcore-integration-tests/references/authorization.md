# Testing authorization, in detail

**Authorization is the thing under test.** Roles, policies, claim requirements, scheme
restrictions, antiforgery — the rules that decide whether a *known* caller is allowed to do
something. Authentication is only stubbed out to get there: the test stack lets any test
declare *who it is*, including roles and arbitrary claims, without a login flow, without real
passwords, and **without booting a new host per identity**.

That distinction drives the design. Because claims are injected directly, every test can name
the exact identity whose permissions it is probing, so the suite can prove the interesting
cases — this role may, that role may not — instead of only proving that anonymous callers are
turned away.

## The shape of it

```
CreateClient()                       AuthenticatedClientBuilder
  .WithJwtAuth(c => c.AsAdmin())     -> ClaimsBuilder produces Claim[]
  .Build()                           -> factory.CreateClient(HandleCookies: true)
                                        + X-Test-Auth: base64(json({scheme, claims}))
                                              |
                                              v
   request -> policy scheme "TestSchemeSelector"
                ForwardDefaultSelector reads the header's scheme
                   |                        |
                   v                        v
            "Bearer"/"Cookies"          no header
            -> TestAuthHandler          -> NoOpAuthHandler
               (stateless, builds          (NoResult -> clean 401
                ClaimsPrincipal)            on challenge)
```

## Why a policy scheme instead of one test scheme

The naive approach registers a single `"Test"` scheme as the default and calls it done. That
breaks anything that looks up a scheme *by name*:

- `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` will not match.
- ASP.NET Core's antiforgery integration checks the **cookie** scheme specifically.
- Endpoints that deliberately accept only one scheme silently accept both.

So the test host registers `TestAuthHandler` **once per real scheme name** (`Cookies`,
`Bearer`) and makes the *default* a policy scheme whose `ForwardDefaultSelector` picks the
real scheme from the request header:

```csharp
services.AddAuthentication(TestAuthSchemes.Selector)
    .AddPolicyScheme(TestAuthSchemes.Selector, TestAuthSchemes.Selector, options =>
    {
        options.ForwardDefaultSelector = context =>
            TestAuthPayload.ReadScheme(context.Request) ?? NoOpAuthHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, NoOpAuthHandler>(NoOpAuthHandler.SchemeName, _ => { })
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(CookieAuthenticationDefaults.AuthenticationScheme, _ => { })
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
```

Only **credential validation** is replaced. Scheme selection, authorization policies, role
checks, and antiforgery all run exactly as they do in production.

## Why the handler must be stateless

`TestAuthHandler.HandleAuthenticateAsync` reads `X-Test-Auth` from the request every single
time and never caches anything on the handler or in DI. That is precisely what allows one
`WebApplicationFactory` to serve every test's identity.

The alternative — registering the identity in the container — means a different identity
requires a different container, which requires `WithWebHostBuilder`, which boots another full
app *and is retained by the parent factory for the rest of the run*. That is the single most
expensive mistake in ASP.NET Core integration testing.

**If you change one thing in this stack, do not make the handler stateful.**

## Why `NoOpAuthHandler` returns `NoResult()`

An anonymous request still needs a default scheme to challenge, or ASP.NET Core throws
"No authenticationScheme was specified, and there was no DefaultChallengeScheme found."
`NoResult()` means "this handler has no opinion", so the pipeline produces a clean **401**
for a protected endpoint. That is what makes `CreateClient().Build()` a valid way to test the
anonymous case.

## Claims: naming the identity whose permissions are under test

`ClaimsBuilder` makes that identity explicit and greppable:

```csharp
claims.AsAdmin()                       // Name + NameIdentifier + Role "administrator"
claims.AsUser("alice")                 // Name + NameIdentifier
claims.WithUserId("42")
claims.WithName("Alice")
claims.WithRole("auditor")
claims.WithClaim("tenant_id", "acme")  // anything your policies read
```

With no claims configured it emits a default non-admin `TestUser`, so
`.WithJwtAuth()` means "some authenticated user".

No registration or login is needed to reach an authorization decision. Keep a small number of
separate tests that exercise the *real* login flow end to end; everything else uses this.

### Testing roles

Assert both directions, or the test proves nothing:

```csharp
var admin = CreateClient().WithJwtAuth(c => c.AsAdmin()).Build();          // expect 2xx
var user  = CreateClient().WithJwtAuth(c => c.AsUser("alice")).Build();    // expect 403
```

### Testing a custom policy

Add the claim the policy reads, then assert 403 when it is absent or wrong:

```csharp
var ok  = CreateClient().WithJwtAuth(c => c.AsUser("alice").WithClaim("tenant_id", "acme")).Build();
var bad = CreateClient().WithJwtAuth(c => c.AsUser("alice").WithClaim("tenant_id", "other")).Build();
```

If both return the same status, the policy is not actually being enforced — that is the bug
this pattern is designed to surface.

### 401 is not 403

Assert them distinctly. **401** means authentication had no identity to hand over. **403**
means a known identity was handed over and *failed the rule*. Only the second one exercises
authorization; a suite that stops at "anonymous is rejected" would still pass with every
policy deleted.

## Adding another scheme

1. Register `TestAuthHandler` under the new real scheme name in `BuildFactory`.
2. Add a `WithXxxAuth(...)` method to `AuthenticatedClientBuilder` that sets that scheme name.

No other change is needed; the forward selector already routes by name.

## CSRF / antiforgery

Cookie-authenticated tests exercise the real antiforgery pipeline. The client is always built
with cookie handling enabled (`HandleCookies = true`), so the antiforgery cookie round-trips
like a browser.

```csharp
await client.WithCsrfTokenAsync();   // GET /antiforgery/token, attach it as a default header
client.WithoutCsrfToken();           // prove protected calls are rejected without it
await client.PostWithCsrfAsync(url, body);  // fetch-if-missing, then POST
```

`WithCsrfTokenAsync` throws if the token endpoint is missing or returns nothing, so a
misconfigured setup fails loudly instead of leaving every request silently unprotected.

Requirements on the app side:

- An endpoint that issues a token from `IAntiforgery.GetAndStoreTokens` — e.g.
  `GET /antiforgery/token` returning `{ token }`.
- `services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN")` and `app.UseAntiforgery()`.

`token` is the only field required. If the response *also* carries a `headerName`,
`WithCsrfTokenAsync` uses it; otherwise it falls back to `X-XSRF-TOKEN`. That fallback must
match the `HeaderName` your app configures — if you set a different one and do not report it
from the endpoint, every cookie-auth write will fail with a 400 that looks like a broken test.

Always test **both** directions. A CSRF test that only proves the happy path passes just as
well when antiforgery is switched off entirely.
