using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

/// <summary>
/// Gatekeeper for every non-public endpoint. Reads the user ID and role that /api/auth/login stored in the
/// server-side session. No session = 401. Wrong role = 403. No roles listed = any signed-in user.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public SessionAuthorizeAttribute(params string[] roles)
    {
        Roles = roles;
    }

    public IReadOnlyList<string> Roles { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetUserId();
        var role = session.GetRole();

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication required",
                Detail = "Log in with POST /api/auth/login before calling this endpoint."
            });
            return;
        }

        if (Roles.Count > 0 && !Roles.Contains(role, StringComparer.Ordinal))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"This endpoint requires the {string.Join(" or ", Roles)} role."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
