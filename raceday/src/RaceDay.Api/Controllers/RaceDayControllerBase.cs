using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class RaceDayControllerBase : ControllerBase
{
    /// <summary>The signed-in user's ID from the server-side session (null when anonymous).</summary>
    protected int? CurrentUserId => HttpContext.Session.GetUserId();

    /// <summary>Builds a ProblemDetails response without needing MVC services (keeps controllers unit-testable).</summary>
    protected static ObjectResult ProblemResult(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };

    protected static ObjectResult NotFoundProblem(string detail) => ProblemResult(StatusCodes.Status404NotFound, "Not found", detail);
    protected static ObjectResult ForbiddenProblem(string detail) => ProblemResult(StatusCodes.Status403Forbidden, "Forbidden", detail);
    protected static ObjectResult ConflictProblem(string detail) => ProblemResult(StatusCodes.Status409Conflict, "Conflict", detail);
    protected static ObjectResult BadRequestProblem(string detail) => ProblemResult(StatusCodes.Status400BadRequest, "Invalid request", detail);
    protected static ObjectResult UnauthorizedProblem() =>
        ProblemResult(StatusCodes.Status401Unauthorized, "Authentication required", "Log in with POST /api/auth/login first.");
}
