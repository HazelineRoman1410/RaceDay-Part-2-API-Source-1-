using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for POST /api/events and PUT /api/events/{id}.</summary>
public class EventRequest
{
    /// <example>Soweto Sunrise 21K</example>
    [Required, StringLength(160, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    /// <example>Half marathon past Vilakazi Street at first light.</example>
    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Start date and time of the event. Must be in the future when creating.</summary>
    [Required]
    public DateTime EventDate { get; set; }

    /// <example>Soweto, Johannesburg</example>
    [Required, StringLength(160, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;

    /// <example>21.1</example>
    [Range(0.1, 9999.99)]
    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    /// <example>Run</example>
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
    public string EventType { get; set; } = string.Empty;
}

/// <summary>Optional filters for GET /api/events.</summary>
public class EventQuery
{
    /// <summary>Run, Walk or Cycle.</summary>
    public string? Type { get; set; }
    /// <summary>Case-insensitive partial match on location, e.g. "Cape".</summary>
    public string? Location { get; set; }
    /// <summary>Partial match on name or description.</summary>
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Include events that have already happened. Defaults to false (upcoming only).</summary>
    public bool IncludePast { get; set; }
}

public record EventSummaryResponse(
    int EventId, string Name, DateTime EventDate, string Location, decimal DistanceKm,
    string EventType, string? BannerImageUrl, int OrganiserId, int EnrolmentCount);

public record EventDetailResponse(
    int EventId, string Name, string Description, DateTime EventDate, string Location, decimal DistanceKm,
    string EventType, string? BannerImageUrl, int OrganiserId, string OrganiserName,
    IReadOnlyList<CategoryResponse> Categories);

/// <summary>Multipart form for POST /api/events/{id}/banner.</summary>
public class BannerUploadRequest
{
    /// <summary>JPEG, PNG or WebP image, 5 MB maximum.</summary>
    [Required]
    public IFormFile File { get; set; } = null!;
}

public record BannerUploadResponse(int EventId, string BannerImageUrl);
