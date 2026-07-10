using System.Security.Claims;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;


/// <summary>
/// Builder for configuring authentication claims.
/// </summary>
public class ClaimsBuilder
{
    private readonly List<Claim> _claims = new();

    /// <summary>
    /// Configures the identity as an administrator.
    /// </summary>
    public ClaimsBuilder AsAdmin()
    {
        _claims.Add(new Claim(ClaimTypes.Name, "Administrator"));
        _claims.Add(new Claim(ClaimTypes.NameIdentifier, "Administrator"));
        _claims.Add(new Claim(ClaimTypes.Role, "administrator"));
        return this;
    }

    /// <summary>
    /// Configures the identity as a specific user.
    /// </summary>
    public ClaimsBuilder AsUser(string nameIdentifier)
    {
        _claims.Add(new Claim(ClaimTypes.Name, nameIdentifier));
        _claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        return this;
    }

    /// <summary>
    /// Sets the user ID (NameIdentifier claim).
    /// </summary>
    public ClaimsBuilder WithUserId(string userId)
    {
        _claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        return this;
    }

    /// <summary>
    /// Sets the user name (Name claim).
    /// </summary>
    public ClaimsBuilder WithName(string name)
    {
        _claims.Add(new Claim(ClaimTypes.Name, name));
        return this;
    }

    /// <summary>
    /// Adds a role claim.
    /// </summary>
    public ClaimsBuilder WithRole(string role)
    {
        _claims.Add(new Claim(ClaimTypes.Role, role));
        return this;
    }

    /// <summary>
    /// Adds a custom claim.
    /// </summary>
    public ClaimsBuilder WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    internal Claim[] Build()
    {
        // If no claims were added, create a default non-admin user
        if (_claims.Count == 0)
        {
            return new[]
            {
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.NameIdentifier, "TestUser")
            };
        }

        return _claims.ToArray();
    }
}