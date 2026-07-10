using System.Security.Claims;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

public class TestAuthClaims
{
    public Claim[] Claims { get; }

    public TestAuthClaims(Claim[] claims)
    {
        Claims = claims;
    }
}