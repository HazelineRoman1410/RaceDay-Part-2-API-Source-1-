using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Finish times and positions: captured by organisers, read by participants.</summary>
[Route("api/results")]
public class ResultsController : RaceDayControllerBase
{
    private static readonly TimeSpan MaxFinishTime = TimeSpan.FromHours(24);
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Capture a finish time and position for an enrolment in an owned event.</summary>
    /// <remarks>Only after the event has started, never for cancelled entries, one result per enrolment, and each position used once per event.</remarks>
    /// <response code="201">Result captured.</response>
    /// <response code="400">Event has not happened yet, entry cancelled, or invalid time.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No enrolment with this ID.</response>
    /// <response code="409">Result already captured, or position already taken.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture([FromBody] ResultRequest request)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var enrolment = await _db.Enrolments
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Participant)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);
        if (enrolment is null) return NotFoundProblem($"Enrolment {request.EnrolmentId} does not exist.");
        if (enrolment.Event.OrganiserId != organiserId) return ForbiddenProblem("You can only capture results for your own events.");
        if (enrolment.Event.EventDate > DateTime.UtcNow) return BadRequestProblem("Results can only be captured after the event starts.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled) return BadRequestProblem("This enrolment was cancelled.");
        if (!IsValidFinishTime(request.FinishTime)) return BadRequestProblem("FinishTime must be between 00:00:01 and 23:59:59.");
        if (enrolment.Result is not null) return ConflictProblem("A result already exists for this enrolment. Use PUT to correct it.");
        if (await PositionTakenAsync(enrolment.EventId, request.Position, exceptResultId: null))
            return ConflictProblem($"Position {request.Position} is already taken in this event.");

        var result = new Result
        {
            EnrolmentId = enrolment.EnrolmentId,
            Enrolment = enrolment,
            FinishTime = request.FinishTime,
            Position = request.Position
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var total = await CountFinishersAsync(enrolment.EventId);
        return Created("/api/results/mine", result.ToResponse(total));
    }

    /// <summary>The signed-in Participant's race history, most recent first.</summary>
    /// <remarks>TotalFinishers lets the UI say "47th out of 312".</remarks>
    /// <response code="200">Array of the caller's results only.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in as an Organiser.</response>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        if (CurrentUserId is not int participantId) return UnauthorizedProblem();

        var results = await _db.Results.AsNoTracking()
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category)
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .ToListAsync();

        var eventIds = results.Select(r => r.Enrolment.EventId).Distinct().ToList();
        var totals = await _db.Results.AsNoTracking()
            .Where(r => eventIds.Contains(r.Enrolment.EventId))
            .GroupBy(r => r.Enrolment.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        return Ok(results.Select(r => r.ToResponse(totals.GetValueOrDefault(r.Enrolment.EventId))));
    }

    /// <summary>Correct a result on an owned event.</summary>
    /// <response code="200">Corrected result.</response>
    /// <response code="400">Invalid time.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No result with this ID.</response>
    /// <response code="409">Position already taken by another finisher.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] ResultUpdateRequest request)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category)
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundProblem($"Result {id} does not exist.");
        if (result.Enrolment.Event.OrganiserId != organiserId) return ForbiddenProblem("You can only correct results for your own events.");
        if (!IsValidFinishTime(request.FinishTime)) return BadRequestProblem("FinishTime must be between 00:00:01 and 23:59:59.");
        if (await PositionTakenAsync(result.Enrolment.EventId, request.Position, exceptResultId: id))
            return ConflictProblem($"Position {request.Position} is already taken in this event.");

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        await _db.SaveChangesAsync();

        return Ok(result.ToResponse(await CountFinishersAsync(result.Enrolment.EventId)));
    }

    private static bool IsValidFinishTime(TimeSpan time) => time > TimeSpan.Zero && time < MaxFinishTime;

    private Task<bool> PositionTakenAsync(int eventId, int position, int? exceptResultId) =>
        _db.Results.AnyAsync(r => r.Enrolment.EventId == eventId && r.Position == position
                                  && (exceptResultId == null || r.ResultId != exceptResultId));

    private Task<int> CountFinishersAsync(int eventId) =>
        _db.Results.CountAsync(r => r.Enrolment.EventId == eventId);
}
