using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

public abstract class TestAuthenticationHandlerBase : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected TestAuthenticationHandlerBase(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected abstract Claim[] CreateClaims();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = CreateClaims();
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}