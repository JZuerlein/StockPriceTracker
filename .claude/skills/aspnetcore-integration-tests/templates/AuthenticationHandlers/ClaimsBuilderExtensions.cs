// TEMPLATE - aspnetcore-integration-tests skill.
// This is the ONE file in AuthenticationHandlers/ you are meant to rewrite. Everything else in
// this folder is domain-free and copies between projects unchanged.

using System.Security.Claims;

namespace YourApp.Tests.Integration.AuthenticationHandlers;

/// <summary>
/// Your application's identity vocabulary, expressed once so tests can name the identity under
/// test instead of assembling claims inline.
///
/// <para>These live outside <see cref="ClaimsBuilder"/> on purpose. A helper called
/// <c>AsAdmin()</c> that hard-codes a role named "administrator" is correct for exactly one
/// application and silently wrong in every other — it still compiles, the request still
/// authenticates, and the authorization assertion quietly stops meaning anything. Keeping the
/// vocabulary here makes it obvious that these names are yours to define.</para>
///
/// <para>Rule of thumb: a method belongs here if renaming a role or policy in the application
/// should change it.</para>
/// </summary>
public static class ClaimsBuilderExtensions
{
    // EXAMPLES — replace these with your application's actual roles, policies and claim types.

    /// <summary>An identity in the application's administrator role.</summary>
    public static ClaimsBuilder AsAdmin(this ClaimsBuilder claims) =>
        claims.AsUser("Administrator").WithRole("administrator");

    /// <summary>
    /// An identity scoped to one tenant — the shape to copy when a policy reads a claim rather
    /// than a role.
    /// </summary>
    public static ClaimsBuilder AsTenantMember(this ClaimsBuilder claims, string userId, string tenantId) =>
        claims.AsUser(userId).WithClaim("tenant_id", tenantId);
}
