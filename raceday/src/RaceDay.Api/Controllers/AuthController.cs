using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Account registration, login and logout. Login creates the server-side session.</summary>
[Route("api/auth")]
public class AuthController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Create an account as an Organiser or a Participant.</summary>
    /// <remarks>Role is whitelisted to the two RaceDay roles. Email is trimmed and lower-cased so it is unique regardless of casing. The password is hashed with salted PBKDF2 and never stored or returned.</remarks>
    /// <response code="201">Account created. Returns the new profile (no password hash).</response>
    /// <response code="400">Validation failed, e.g. weak password or unknown role.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!Roles.IsValid(request.Role))
            return BadRequestProblem("Role must be Organiser or Participant.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return ConflictProblem("An account with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = request.Role,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/profile", user.ToResponse());
    }

    /// <summary>Verify credentials and start a server-side session.</summary>
    /// <remarks>On success the session stores the user ID and role; the browser keeps only the HttpOnly .RaceDay.Session cookie. In Swagger, call this first and every later request is authenticated automatically.</remarks>
    /// <response code="200">Signed in. Returns the profile including the role.</response>
    /// <response code="401">Email or password is wrong (deliberately not saying which).</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            return ProblemResult(StatusCodes.Status401Unauthorized, "Invalid credentials", "Email or password is incorrect.");

        HttpContext.Session.SignIn(user);
        return Ok(user.ToResponse());
    }

    /// <summary>End the current session.</summary>
    /// <response code="204">Session cleared.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete(SessionKeys.CookieName);
        return NoContent();
    }
}
