namespace RaceDay.Api.Models;

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = CategoryTypes.Distance;
    public byte? MinimumAge { get; set; }
    public byte? MaximumAge { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal EntryFee { get; set; }

    public Event Event { get; set; } = null!;
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
