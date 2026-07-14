using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace StockPriceTracker.Security;

/// <summary>
/// Validates the antiforgery token for cookie-authenticated requests only. CSRF protection is
/// relevant to cookie auth — a browser attaches cookies automatically — but not to bearer tokens,
/// which a browser never sends on its own. Requests authenticated by any other scheme (e.g. JWT)
/// pass through untouched.
/// </summary>
public sealed class CookieAntiforgeryFilter : IEndpointFilter
{
    private readonly IAntiforgery _antiforgery;

    public CookieAntiforgeryFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (http.User.Identity?.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme)
        {
            try
            {
                await _antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Invalid or missing antiforgery token.");
            }
        }

        return await next(context);
    }
}
