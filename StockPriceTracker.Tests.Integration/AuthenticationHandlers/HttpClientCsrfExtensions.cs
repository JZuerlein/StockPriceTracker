using System.Net.Http.Json;

namespace StockPriceTracker.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Extension methods for HttpClient to handle CSRF tokens.
/// </summary>
public static class HttpClientCsrfExtensions
{
    private const string CsrfHeaderName = "X-XSRF-TOKEN";
    private const string CsrfEndpoint = "/antiforgery/token";

    /// <summary>
    /// Fetches a CSRF token and adds it to the client's default headers.
    /// All subsequent requests will include the token automatically.
    /// Throws if the token endpoint is missing or returns no token, so a misconfigured
    /// CSRF setup fails loudly instead of leaving requests silently unprotected.
    /// </summary>
    public static async Task<HttpClient> WithCsrfTokenAsync(this HttpClient client)
    {
        var response = await client.GetAsync(CsrfEndpoint);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<AntiforgeryTokenResponse>(content,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrEmpty(tokenResponse?.Token))
            throw new InvalidOperationException($"Antiforgery endpoint '{CsrfEndpoint}' returned no token.");

        // Use the header name from the server response, fall back to default if not provided
        var headerName = !string.IsNullOrEmpty(tokenResponse.HeaderName)
            ? tokenResponse.HeaderName
            : CsrfHeaderName;

        client.DefaultRequestHeaders.Remove(headerName);
        client.DefaultRequestHeaders.Add(headerName, tokenResponse.Token);

        return client;
    }

    /// <summary>
    /// Removes the CSRF token from the client's default headers.
    /// Use this to test that CSRF protection rejects requests without tokens.
    /// </summary>
    public static HttpClient WithoutCsrfToken(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(CsrfHeaderName);
        return client;
    }

    /// <summary>
    /// Checks if the client has a CSRF token in its default headers.
    /// </summary>
    public static bool HasCsrfToken(this HttpClient client)
    {
        return client.DefaultRequestHeaders.Contains(CsrfHeaderName);
    }

    /// <summary>
    /// Posts JSON content with CSRF token handling.
    /// Fetches token if not already present in headers.
    /// </summary>
    public static async Task<HttpResponseMessage> PostWithCsrfAsync<T>(
        this HttpClient client,
        string requestUri,
        T content)
    {
        if (!client.HasCsrfToken())
        {
            await client.WithCsrfTokenAsync();
        }

        return await client.PostAsJsonAsync(requestUri, content);
    }

    private class AntiforgeryTokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string HeaderName { get; set; } = string.Empty;
    }
}