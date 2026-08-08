using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// The wire format for a test identity carried on a request header. The single test host serves
/// many identities across many tests; rather than baking an identity into the DI container (which
/// forces a new host per identity), each request declares who it is and under which real scheme.
/// The header value is a base64-encoded JSON payload of the scheme name plus the claims to emit.
/// </summary>
public static class TestAuthPayload
{
    /// <summary>Header the test client sets and the stateless handler reads the identity from.</summary>
    public const string HeaderName = "X-Test-Auth";

    public static string Encode(string scheme, IEnumerable<Claim> claims)
    {
        var payload = new Payload(
            scheme,
            claims.Select(c => new ClaimData(c.Type, c.Value)).ToArray());
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static Payload Decode(string raw)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        return JsonSerializer.Deserialize<Payload>(json)
               ?? throw new InvalidOperationException("Test auth payload deserialized to null.");
    }

    /// <summary>
    /// Returns the scheme a request declares, or <c>null</c> when the request carries no test
    /// identity. Used by the policy scheme's forward selector to route to the real scheme and by
    /// the stateless handler to short-circuit anonymous requests.
    /// </summary>
    public static string? ReadScheme(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        return Decode(raw!).Scheme;
    }

    public sealed record Payload(string Scheme, ClaimData[] Claims)
    {
        public Claim[] ToClaims() => Claims.Select(c => new Claim(c.Type, c.Value)).ToArray();
    }

    public sealed record ClaimData(string Type, string Value);
}
