// TEMPLATE - aspnetcore-integration-tests skill. Retarget the YourApp namespace, and see
// "Adapting to your project" in SKILL.md for what else is project-specific in this file.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;

namespace YourApp.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Builder for creating authenticated HTTP clients with fluent configuration.
/// Supports both JWT and Cookie authentication.
/// For CSRF token handling, use the HttpClient extension methods after building.
///
/// <para>Crucially, this does NOT create a new <see cref="WebApplicationFactory{T}"/> per client.
/// It reuses the fixture's single host and expresses the identity as a per-request header, so a
/// suite of N tests runs against one host rather than N in-memory servers.</para>
/// </summary>
public class AuthenticatedClientBuilder
{
    private readonly WebApplicationFactory<TestProgram> _factory;
    private AuthScheme _authScheme = AuthScheme.None;
    private readonly ClaimsBuilder _claimsBuilder = new();

    internal AuthenticatedClientBuilder(WebApplicationFactory<TestProgram> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Configures the client to use JWT Bearer authentication.
    /// JWT auth does not require CSRF tokens.
    /// </summary>
    public AuthenticatedClientBuilder WithJwtAuth(Action<ClaimsBuilder>? configureClaims = null)
    {
        _authScheme = AuthScheme.Jwt;
        configureClaims?.Invoke(_claimsBuilder);
        return this;
    }

    /// <summary>
    /// Configures the client to use Cookie authentication.
    /// For CSRF protection, call client.WithCsrfTokenAsync() after building.
    /// </summary>
    public AuthenticatedClientBuilder WithCookieAuth(Action<ClaimsBuilder>? configureClaims = null)
    {
        _authScheme = AuthScheme.Cookie;
        configureClaims?.Invoke(_claimsBuilder);
        return this;
    }

    /// <summary>
    /// Builds and returns the configured HTTP client.
    /// For cookie auth with CSRF, call client.WithCsrfTokenAsync() after building.
    /// </summary>
    public HttpClient Build()
    {
        // Cookie handling is always enabled so the CSRF dance works; it is harmless for JWT.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        if (_authScheme == AuthScheme.None)
        {
            // No identity: requests carry no X-Test-Auth header, so the policy scheme forwards
            // them to the no-op handler and protected endpoints answer 401.
            return client;
        }

        var schemeName = _authScheme == AuthScheme.Jwt
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;

        var claims = _claimsBuilder.Build();
        client.DefaultRequestHeaders.Add(TestAuthPayload.HeaderName, TestAuthPayload.Encode(schemeName, claims));

        return client;
    }

    private enum AuthScheme
    {
        None,
        Jwt,
        Cookie
    }
}
