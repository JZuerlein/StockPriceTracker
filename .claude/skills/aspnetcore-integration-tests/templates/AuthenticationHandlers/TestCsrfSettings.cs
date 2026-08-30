// TEMPLATE - aspnetcore-integration-tests skill.
// One of two files in AuthenticationHandlers/ you are meant to edit (the other is
// ClaimsBuilderExtensions.cs). Both values must match your application, not this default.

namespace YourApp.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Where the antiforgery token comes from and how it is sent back. Both values are wrong by
/// default for any app that does not follow the ASP.NET Core convention, and both fail in a
/// misleading way: the endpoint 404s, or the token is attached under a header the app never
/// reads, and every cookie-authenticated write returns 400 as though the test were broken.
///
/// <para>These are constants rather than settable properties on purpose — they are a property
/// of the application under test, fixed for the whole run, not something an individual test
/// should be able to change out from under the others.</para>
/// </summary>
public static class TestCsrfSettings
{
    /// <summary>
    /// Route that issues a token via <c>IAntiforgery.GetAndStoreTokens</c>. The response needs a
    /// <c>token</c> field; a <c>headerName</c> field is optional and wins over
    /// <see cref="HeaderName"/> when present.
    /// </summary>
    public const string TokenEndpoint = "/antiforgery/token";

    /// <summary>
    /// Must equal the app's <c>services.AddAntiforgery(o =&gt; o.HeaderName = ...)</c>. Used only
    /// when the token endpoint does not report a header name of its own.
    /// </summary>
    public const string HeaderName = "X-XSRF-TOKEN";
}
