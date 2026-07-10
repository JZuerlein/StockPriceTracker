using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Builder for creating authenticated HTTP clients with fluent configuration.
/// Supports both JWT and Cookie authentication.
/// For CSRF token handling, use the HttpClient extension methods after building.
/// </summary>
public class AuthenticatedClientBuilder
{
    private readonly WebApplicationFactory<Program> _factory;
    private AuthScheme _authScheme = AuthScheme.None;
    private readonly ClaimsBuilder _claimsBuilder = new();

    internal AuthenticatedClientBuilder(WebApplicationFactory<Program> factory)
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
        if (_authScheme == AuthScheme.None)
        {
            return _factory.CreateClient();
        }

        var claims = _claimsBuilder.Build();
        var factory = ConfigureAuthentication(_factory, _authScheme, claims);

        // Create client with cookie handling enabled for CSRF to work
        var options = new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        };

        return factory.CreateClient(options);
    }

    private static WebApplicationFactory<Program> ConfigureAuthentication(
        WebApplicationFactory<Program> factory,
        AuthScheme scheme,
        Claim[] claims)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var schemeName = scheme == AuthScheme.Jwt
                    ? JwtBearerDefaults.AuthenticationScheme
                    : CookieAuthenticationDefaults.AuthenticationScheme;

                // Register the claims for the test auth handler
                services.AddSingleton(new TestAuthClaims(claims));

                // Remove existing scheme registrations so the test handler can replace them
                services.Where(d => d.ServiceType == typeof(IConfigureOptions<AuthenticationOptions>))
                    .ToList()
                    .ForEach(d => services.Remove(d));

                services.AddAuthentication(defaultScheme: schemeName)
                    .AddScheme<AuthenticationSchemeOptions, ConfigurableTestAuthHandler>(
                        schemeName,
                        options => { });
            });
        });
    }

    private enum AuthScheme
    {
        None,
        Jwt,
        Cookie
    }
}