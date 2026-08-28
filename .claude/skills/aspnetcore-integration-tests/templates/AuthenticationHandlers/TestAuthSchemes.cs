// TEMPLATE - aspnetcore-integration-tests skill.
// Replace: YourApp.Tests.Integration -> your test namespace, AppDbContext -> your DbContext,
// Stock/StockRequest -> your entities, TestProgram -> your entry point if you use `Program`.

namespace YourApp.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Scheme names for the test authentication setup.
/// </summary>
public static class TestAuthSchemes
{
    /// <summary>
    /// The default policy (selector) scheme. It forwards each request to the real scheme it
    /// declares on the <c>X-Test-Auth</c> header, or to the no-op scheme when none is present.
    /// </summary>
    public const string Selector = "TestSchemeSelector";
}
