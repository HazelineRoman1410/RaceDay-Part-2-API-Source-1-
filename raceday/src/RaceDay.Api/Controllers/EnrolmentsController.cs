using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Participants entering events, and organisers viewing who entered.</summary>
[Route("api/enrolments")]
public class EnrolmentsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enter the signed-in Participant into an event in a chosen category.</summary>
    /// <remarks>Records the Participant + Event + Category link immediately with status Pending. The participant ID comes from the session.</remarks>
    /// <response code="201">Enrolment recorded.</response>
    /// <response code="400">Category belongs to a different event, or entries are closed.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in as an Organiser.</response>
    /// <response code="404">Event or category does not exist.</response>
    /// <response code="409">Already entered this event.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol([FromBody] EnrolmentRequest request)
    {
        if (CurrentUserId is not int participantId) return UnauthorizedProblem();

        var participant = await _db.Users.FindAsync(participantId);
        if (participant is null) return UnauthorizedProblem();

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == request.EventId);
        if (ev is null) return NotFoundProblem($"Event {request.EventId} does not exist.");

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null) return NotFoundProblem($"Category {request.CategoryId} does not exist.");
        if (category.EventId != ev.EventId) return BadRequestProblem("That category does not belong to this event.");
        if (ev.EventDate <= DateTime.UtcNow) return BadRequestProblem("Entries for this event have closed.");

        if (await _db.Enrolments.AnyAsync(e => e.ParticipantId == participantId && e.EventId == ev.EventId))
            return ConflictProblem("You have already entered this event.");

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            Participant = participant,
            EventId = ev.EventId,
            Event = ev,
            CategoryId = category.CategoryId,
            Category = category,
            Status = EnrolmentStatuses.Pending,
            EnrolledAt = DateTime.UtcNow
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        return Created("/api/enrolments/mine", enrolment.ToResponse());
    }

    /// <summary>The signed-in Participant's own enrolments, soonest event first.</summary>
    /// <response code="200">Array of the caller's enrolments only.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in as an Organiser.</response>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        if (CurrentUserId is not int participantId) return UnauthorizedProblem();

        var enrolments = await WithDetails()
            .Where(e => e.ParticipantId == participantId)
            .OrderBy(e => e.Event.EventDate)
            .ToListAsync();
        return Ok(enrolments.Select(e => e.ToResponse()));
    }

    /// <summary>Everyone entered in an event the signed-in Organiser owns.</summary>
    /// <response code="200">Array of enrolments with participant, category and status.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("/api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} does not exist.");
        if (ev.OrganiserId != organiserId) return ForbiddenProblem("You can only view entrants for events you created.");

        var enrolments = await WithDetails()
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.Participant.LastName).ThenBy(e => e.Participant.FirstName)
            .ToListAsync();
        return Ok(enrolments.Select(e => e.ToResponse()));
    }

    private IQueryable<Enrolment> WithDetails() =>
        _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category);
}
