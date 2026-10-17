using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Support;

/// <summary>IDs of the known rows every test starts from.</summary>
public sealed record SeededIds(
    int ThandiOrganiserId, int LeboOrganiserId, int AidenParticipantId, int NandiParticipantId,
    int UpcomingEventId, int UpcomingCategoryId,
    int PastEventId, int PastCategoryId, int AidenPastEnrolmentId, int NandiPastEnrolmentId,
    int LebosEventId, int LebosCategoryId);

/// <summary>
/// Small, readable fixture: two organisers, two participants, Thandi's upcoming and past events,
/// and one upcoming event owned by Lebo (used to prove organisers cannot touch each other's events).
/// </summary>
public static class TestData
{
    public const string Password = "RaceDay@2026";

    public static SeededIds Seed(RaceDayDbContext db, IPasswordHasher hasher)
    {
        var now = DateTime.UtcNow;
        User NewUser(string first, string last, string role) => new()
        {
            FirstName = first, LastName = last, Role = role, CreatedAt = now,
            Email = $"{first.ToLowerInvariant()}@raceday.example",
            PasswordHash = hasher.Hash(Password)
        };

        var thandi = NewUser("Thandi", "Mokoena", Roles.Organiser);
        var lebo = NewUser("Lebo", "Naidoo", Roles.Organiser);
        var aiden = NewUser("Aiden", "Jacobs", Roles.Participant);
        var nandi = NewUser("Nandi", "Dlamini", Roles.Participant);

        var half21K = new Category { Name = "Half 21K", CategoryType = CategoryTypes.Distance, DistanceKm = 21.1m, EntryFee = 250 };
        var upcoming = new Event
        {
            Organiser = thandi, Name = "Soweto Sunrise 21K", Description = "Half marathon past Vilakazi Street.",
            EventDate = now.AddDays(30), Location = "Soweto", DistanceKm = 21.1m, EventType = EventTypes.Run,
            CreatedAt = now, Categories = { half21K }
        };

        var senior10K = new Category { Name = "Senior 10K", CategoryType = CategoryTypes.Age, MinimumAge = 20, MaximumAge = 39, DistanceKm = 10, EntryFee = 150 };
        var past = new Event
        {
            Organiser = thandi, Name = "Golden Hour 10K", Description = "Sunset road race through Johannesburg South.",
            EventDate = now.AddDays(-7), Location = "Johannesburg South", DistanceKm = 10, EventType = EventTypes.Run,
            CreatedAt = now.AddDays(-60), Categories = { senior10K }
        };

        var classic42K = new Category { Name = "Classic 42K", CategoryType = CategoryTypes.Distance, DistanceKm = 42, EntryFee = 450 };
        var lebos = new Event
        {
            Organiser = lebo, Name = "Cape Peninsula Cycle Classic", Description = "Coastal cycle route for club riders.",
            EventDate = now.AddDays(45), Location = "Cape Town", DistanceKm = 42, EventType = EventTypes.Cycle,
            CreatedAt = now, Categories = { classic42K }
        };

        var aidenPast = new Enrolment { Participant = aiden, Event = past, Category = senior10K, Status = EnrolmentStatuses.Confirmed, EnrolledAt = now.AddDays(-30) };
        var nandiPast = new Enrolment { Participant = nandi, Event = past, Category = senior10K, Status = EnrolmentStatuses.Confirmed, EnrolledAt = now.AddDays(-29) };

        db.AddRange(thandi, lebo, aiden, nandi, upcoming, past, lebos, aidenPast, nandiPast);
        db.SaveChanges();

        return new SeededIds(
            thandi.UserId, lebo.UserId, aiden.UserId, nandi.UserId,
            upcoming.EventId, half21K.CategoryId,
            past.EventId, senior10K.CategoryId, aidenPast.EnrolmentId, nandiPast.EnrolmentId,
            lebos.EventId, classic42K.CategoryId);
    }
}
