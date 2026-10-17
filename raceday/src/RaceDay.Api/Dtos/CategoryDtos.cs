using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for POST /api/events/{id}/categories.</summary>
public class CategoryRequest : IValidatableObject
{
    /// <example>Under 20</example>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Age or Distance.</summary>
    /// <example>Age</example>
    [Required, RegularExpression("^(Age|Distance)$", ErrorMessage = "CategoryType must be Age or Distance.")]
    public string CategoryType { get; set; } = string.Empty;

    /// <example>12</example>
    [Range(0, 120)]
    public byte? MinimumAge { get; set; }

    /// <example>19</example>
    [Range(0, 120)]
    public byte? MaximumAge { get; set; }

    /// <example>10</example>
    [Range(0.1, 9999.99)]
    public decimal? DistanceKm { get; set; }

    /// <example>120</example>
    [Range(0, 100000)]
    public decimal EntryFee { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CategoryType == "Age" && MinimumAge is null && MaximumAge is null)
            yield return new ValidationResult("An Age category needs a MinimumAge, a MaximumAge, or both.",
                new[] { nameof(MinimumAge), nameof(MaximumAge) });

        if (MinimumAge is not null && MaximumAge is not null && MinimumAge > MaximumAge)
            yield return new ValidationResult("MinimumAge cannot be greater than MaximumAge.",
                new[] { nameof(MinimumAge), nameof(MaximumAge) });

        if (CategoryType == "Distance" && DistanceKm is null)
            yield return new ValidationResult("A Distance category needs DistanceKm.", new[] { nameof(DistanceKm) });
    }
}

public record CategoryResponse(
    int CategoryId, int EventId, string Name, string CategoryType,
    byte? MinimumAge, byte? MaximumAge, decimal? DistanceKm, decimal EntryFee);
