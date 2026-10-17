using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Browse events (public) and manage owned events (Organiser).</summary>
[Route("api/events")]
public class EventsController : RaceDayControllerBase
{
    public const long MaxBannerBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedBannerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly RaceDayDbContext _db;
    private readonly IBannerStorage _bannerStorage;

    public EventsController(RaceDayDbContext db, IBannerStorage bannerStorage)
    {
        _db = db;
        _bannerStorage = bannerStorage;
    }

    /// <summary>List events, upcoming first, with optional filters.</summary>
    /// <remarks>Public. Filters: type (Run/Walk/Cycle), location, search, from, to, includePast. EnrolmentCount excludes cancelled entries.</remarks>
    /// <response code="200">Array of event summaries (may be empty).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] EventQuery query)
    {
        var events = _db.Events.AsNoTracking().AsQueryable();

        if (!query.IncludePast) events = events.Where(e => e.EventDate >= DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(query.Type)) events = events.Where(e => e.EventType == query.Type);
        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            var location = query.Location.Trim().ToLower();
            events = events.Where(e => e.Location.ToLower().Contains(location));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            events = events.Where(e => e.Name.ToLower().Contains(search) || e.Description.ToLower().Contains(search));
        }
        if (query.From is DateTime from) events = events.Where(e => e.EventDate >= from);
        if (query.To is DateTime to) events = events.Where(e => e.EventDate <= to);

        var result = await events
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryResponse(
                e.EventId, e.Name, e.EventDate, e.Location, e.DistanceKm, e.EventType, e.BannerImageUrl,
                e.OrganiserId, e.Enrolments.Count(en => en.Status != EnrolmentStatuses.Cancelled)))
            .ToListAsync();

        return Ok(result);
    }

    /// <summary>Event details with its categories.</summary>
    /// <response code="200">The event and its categories.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await LoadEventAsync(id, tracking: false);
        return ev is null ? NotFoundProblem($"Event {id} does not exist.") : Ok(ev.ToDetail());
    }

    /// <summary>Create an event owned by the signed-in Organiser.</summary>
    /// <remarks>The organiser is taken from the session, never from the body, so nobody can create events on someone else's behalf.</remarks>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed or date is in the past.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in as a Participant.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] EventRequest request)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();
        if (request.EventDate <= DateTime.UtcNow) return BadRequestProblem("EventDate must be in the future.");

        var organiser = await _db.Users.FindAsync(organiserId);
        if (organiser is null) return UnauthorizedProblem();

        var ev = new Event { Organiser = organiser, OrganiserId = organiserId, CreatedAt = DateTime.UtcNow };
        Apply(ev, request);
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ev.ToDetail());
    }

    /// <summary>Update an event the signed-in Organiser owns.</summary>
    /// <response code="200">Updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var ev = await LoadEventAsync(id, tracking: true);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != organiserId) return ForbiddenProblem("You can only edit events you created.");

        Apply(ev, request);
        await _db.SaveChangesAsync();
        return Ok(ev.ToDetail());
    }

    /// <summary>Delete an event the signed-in Organiser owns.</summary>
    /// <remarks>Blocked with 409 once anyone has enrolled, so participant history is never silently destroyed. Categories, weather snapshots and route points of an un-entered event are removed with it.</remarks>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No event with this ID.</response>
    /// <response code="409">Event already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var ev = await _db.Events
            .Include(e => e.Categories)
            .Include(e => e.WeatherSnapshots)
            .Include(e => e.RoutePoints)
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != organiserId) return ForbiddenProblem("You can only delete events you created.");
        if (await _db.Enrolments.AnyAsync(e => e.EventId == id))
            return ConflictProblem("This event already has enrolments and cannot be deleted.");

        _db.Categories.RemoveRange(ev.Categories);
        _db.WeatherSnapshots.RemoveRange(ev.WeatherSnapshots);
        _db.RoutePoints.RemoveRange(ev.RoutePoints);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Upload a banner image for an owned event.</summary>
    /// <remarks>multipart/form-data with a single "file" field. JPEG, PNG or WebP up to 5 MB. Part 2 stores it through the IBannerStorage adapter (local disk); Part 3 swaps in Azure Blob Storage behind the same endpoint.</remarks>
    /// <response code="200">Banner saved; returns its URL.</response>
    /// <response code="400">No file, or not a supported image type.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Participant, or another organiser's event.</response>
    /// <response code="404">No event with this ID.</response>
    /// <response code="413">File larger than 5 MB.</response>
    [HttpPost("{id:int}/banner")]
    [SessionAuthorize(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(BannerUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadBanner(int id, [FromForm] BannerUploadRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not int organiserId) return UnauthorizedProblem();

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != organiserId) return ForbiddenProblem("You can only change banners on events you created.");

        var file = request.File;
        if (file is null || file.Length == 0) return BadRequestProblem("Attach an image in the 'file' field.");
        if (file.Length > MaxBannerBytes)
            return ProblemResult(StatusCodes.Status413PayloadTooLarge, "File too large", "Banners must be 5 MB or smaller.");
        if (!AllowedBannerTypes.TryGetValue(file.ContentType, out var extension))
            return BadRequestProblem("Banner must be a JPEG, PNG or WebP image.");

        await using var stream = file.OpenReadStream();
        ev.BannerImageUrl = await _bannerStorage.SaveBannerAsync(id, stream, file.ContentType, extension, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new BannerUploadResponse(id, ev.BannerImageUrl));
    }

    private Task<Event?> LoadEventAsync(int id, bool tracking)
    {
        IQueryable<Event> query = _db.Events.Include(e => e.Organiser).Include(e => e.Categories);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(e => e.EventId == id);
    }

    private static void Apply(Event ev, EventRequest request)
    {
        ev.Name = request.Name.Trim();
        ev.Description = request.Description.Trim();
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType;
    }
}
