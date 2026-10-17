namespace RaceDay.Api.Models;

/// <summary>Race-day weather reading. Table exists from Part 1; endpoints arrive with the Part 3 race-day prep page.</summary>
public class WeatherSnapshot
{
    public int WeatherId { get; set; }
    public int EventId { get; set; }
    public DateTime ObservedAt { get; set; }
    public decimal TemperatureC { get; set; }
    public string Conditions { get; set; } = string.Empty;
    public decimal? WindKph { get; set; }
    public byte? RainChancePercent { get; set; }

    public Event Event { get; set; } = null!;
}
