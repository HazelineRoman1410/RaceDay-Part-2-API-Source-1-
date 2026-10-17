using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>The signed-in user's own profile. Both roles.</summary>
[Route("api/profile")]
[SessionAuthorize]
public class ProfileController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public ProfileController(RaceDayDbContext db) => _db = db;

    /// <summary>Read the signed-in user's profile.</summary>
    /// <response code="200">The profile.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get()
    {
        if (CurrentUserId is not int userId) return UnauthorizedProblem();
        var user = await _db.Users.FindAsync(userId);
        return user is null ? UnauthorizedProblem() : Ok(user.ToResponse());
    }

    /// <summary>Update name, phone and profile image URL. Email and role cannot be changed here.</summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not signed in.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request)
    {
        if (CurrentUserId is not int userId) return UnauthorizedProblem();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return UnauthorizedProblem();

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
        await _db.SaveChangesAsync();

        return Ok(user.ToResponse());
    }
}
