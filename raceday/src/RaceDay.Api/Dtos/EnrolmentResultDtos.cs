using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for POST /api/enrolments. The participant comes from the session, never the body.</summary>
public class EnrolmentRequest
{
    /// <example>3</example>
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    /// <example>5</example>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

public record EnrolmentResponse(
    int EnrolmentId, int ParticipantId, string ParticipantName, int EventId, string EventName,
    DateTime EventDate, int CategoryId, string CategoryName, string Status, DateTime EnrolledAt);

/// <summary>Body for POST /api/results.</summary>
public class ResultRequest
{
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int EnrolmentId { get; set; }

    /// <summary>hh:mm:ss, e.g. "00:48:22".</summary>
    /// <example>00:48:22</example>
    [Required]
    public TimeSpan FinishTime { get; set; }

    /// <example>47</example>
    [Range(1, int.MaxValue)]
    public int Position { get; set; }
}

/// <summary>Body for PUT /api/results/{id}.</summary>
public class ResultUpdateRequest
{
    /// <example>00:48:05</example>
    [Required]
    public TimeSpan FinishTime { get; set; }

    /// <example>46</example>
    [Range(1, int.MaxValue)]
    public int Position { get; set; }
}

/// <summary>A finish result. TotalFinishers powers "47th out of 312" on the Part 3 My Results page.</summary>
public record ResultResponse(
    int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate, string CategoryName,
    string ParticipantName, TimeSpan FinishTime, int Position, int TotalFinishers);
