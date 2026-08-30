// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace, and see
// "Adapting to your project" in SKILL.md for what else is project-specific in this file.

using System.Security.Claims;

namespace YourApp.Tests.Integration.AuthenticationHandlers;


/// <summary>
/// Builder for configuring authentication claims.
///
/// <para>This type is deliberately domain-free: it knows how to attach claims, not what your
/// application's roles or claim types mean. Put project vocabulary — <c>AsAdmin()</c>,
/// <c>AsTenantOwner(id)</c>, and so on — in extension methods instead, so this file stays
/// copyable between projects unchanged. See <c>ClaimsBuilderExtensions.cs</c>.</para>
/// </summary>
public class ClaimsBuilder
{
    private readonly List<Claim> _claims = new();

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
        // No claims configured means "some authenticated user, carrying no roles or claims" —
        // the baseline a `.WithJwtAuth()` with no arguments should get. Anything your policies
        // actually read must be stated explicitly by the test.
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