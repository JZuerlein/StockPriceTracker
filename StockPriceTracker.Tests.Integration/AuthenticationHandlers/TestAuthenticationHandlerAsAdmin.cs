using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

public class TestAuthenticationHandlerAsAdmin : TestAuthenticationHandlerBase
{
    public TestAuthenticationHandlerAsAdmin(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Claim[] CreateClaims()
    {
        return new[]
        {
            new Claim(ClaimTypes.Name, "administrator"),
            new Claim(ClaimTypes.NameIdentifier, "administrator"),
        };
    }
}