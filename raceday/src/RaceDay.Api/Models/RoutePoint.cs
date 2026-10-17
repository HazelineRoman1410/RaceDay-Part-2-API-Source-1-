namespace RaceDay.Api.Models;

/// <summary>Ordered GPS point on an event route. Table exists from Part 1; consumed by the Part 3 route map.</summary>
public class RoutePoint
{
    public int RoutePointId { get; set; }
    public int EventId { get; set; }
    public int SequenceNo { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? ElevationMetres { get; set; }

    public Event Event { get; set; } = null!;
}
