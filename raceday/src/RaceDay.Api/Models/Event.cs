namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = EventTypes.Run;
    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    public ICollection<WeatherSnapshot> WeatherSnapshots { get; set; } = new List<WeatherSnapshot>();
    public ICollection<RoutePoint> RoutePoints { get; set; } = new List<RoutePoint>();
}
