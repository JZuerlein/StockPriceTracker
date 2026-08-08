using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Stateless test authentication handler. One instance of this handler is registered per real
/// scheme (Cookie, Bearer) on a single shared host; the identity is never cached on the handler
/// or in DI — it is read from the request on every call. That is what lets one
/// <c>WebApplicationFactory</c> serve every test's identity without booting a host per test.
///
/// <para><c>Scheme.Name</c> is the real scheme, because the policy scheme forwarded the request
/// here based on the header. Only credential validation is swapped; scheme-name lookups
/// (<c>[Authorize(AuthenticationSchemes = ...)]</c>, antiforgery's cookie-scheme check) stay
/// faithful.</para>
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestAuthPayload.HeaderName, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            // No test identity on the request: anonymous.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var payload = TestAuthPayload.Decode(raw!);
        var identity = new ClaimsIdentity(payload.ToClaims(), Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
