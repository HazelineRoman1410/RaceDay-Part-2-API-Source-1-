using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for POST /api/auth/register.</summary>
public class RegisterRequest
{
    /// <example>Sipho</example>
    [Required, StringLength(80, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    /// <example>Khumalo</example>
    [Required, StringLength(80, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    /// <example>sipho@raceday.example</example>
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters with a letter and a digit. Stored only as a PBKDF2 hash.</summary>
    /// <example>StrongPass#1</example>
    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one digit.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Organiser or Participant. No other value is accepted.</summary>
    /// <example>Participant</example>
    [Required, RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Role must be Organiser or Participant.")]
    public string Role { get; set; } = string.Empty;

    /// <example>0825550199</example>
    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}

/// <summary>Body for POST /api/auth/login.</summary>
public class LoginRequest
{
    /// <example>thandi.organiser@raceday.example</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>RaceDay@2026</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Body for PUT /api/profile.</summary>
public class UpdateProfileRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Url, StringLength(500)]
    public string? ProfileImageUrl { get; set; }
}

/// <summary>Public view of an account. Never includes the password hash.</summary>
public record UserResponse(
    int UserId, string FirstName, string LastName, string Email, string Role,
    string? Phone, string? ProfileImageUrl, DateTime CreatedAt);
