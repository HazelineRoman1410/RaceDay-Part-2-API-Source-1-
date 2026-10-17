namespace RaceDay.Api.Models;

/// <summary>The only two roles a RaceDay account can hold. Anything else is rejected at registration.</summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static readonly IReadOnlyList<string> All = new[] { Organiser, Participant };

    public static bool IsValid(string? role) => role is not null && All.Contains(role, StringComparer.Ordinal);
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
}

public static class CategoryTypes
{
    public const string Age = "Age";
    public const string Distance = "Distance";
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}
