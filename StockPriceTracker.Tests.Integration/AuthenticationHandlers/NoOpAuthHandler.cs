using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Default scheme for requests that carry no test identity. It never authenticates anyone
/// (returns <see cref="AuthenticateResult.NoResult"/>), which makes it a valid default challenge
/// scheme: an unauthenticated request to a protected endpoint gets a clean 401 instead of
/// throwing because no challenge scheme was registered. Clients that supply an identity replace
/// this default via <see cref="AuthenticatedClientBuilder"/>.
/// </summary>
public class NoOpAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Anonymous";

    public NoOpAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
