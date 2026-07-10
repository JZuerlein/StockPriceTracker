using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

public class ConfigurableTestAuthHandler : TestAuthenticationHandlerBase
{
    private readonly TestAuthClaims _testAuthClaims;

    public ConfigurableTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthClaims testAuthClaims)
        : base(options, logger, encoder)
    {
        _testAuthClaims = testAuthClaims;
    }

    protected override Claim[] CreateClaims()
    {
        return _testAuthClaims.Claims;
    }
}
