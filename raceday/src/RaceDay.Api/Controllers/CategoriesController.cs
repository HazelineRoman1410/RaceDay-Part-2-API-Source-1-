using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Age or distance categories that belong to one event.</summary>
[Route("api/events/{eventId:int}/categories")]
public class CategoriesController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>List the categories available for an event.</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFoundProblem($"Event {eventId} does not exist.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return Ok(categories.Select(c => c.ToResponse()));
    }

    /// <summary>Add an age or distance category to an owned event.</summary>
    /// <remarks>Age categories need at least one age bound; Distance categories need DistanceKm. Names are unique per event.</remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No event with this ID.</response>
    /// <response code="409">A category with this name already exists on the event.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CategoryRequest request)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} does not exist.");
        if (ev.OrganiserId != organiserId) return ForbiddenProblem("You can only add categories to events you created.");

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return ConflictProblem($"'{name}' already exists on this event.");

        var category = new Category
        {
            EventId = eventId,
            Name = name,
            CategoryType = request.CategoryType,
            MinimumAge = request.MinimumAge,
            MaximumAge = request.MaximumAge,
            DistanceKm = request.DistanceKm,
            EntryFee = request.EntryFee
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", category.ToResponse());
    }
}
