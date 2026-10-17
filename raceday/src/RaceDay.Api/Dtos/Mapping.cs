using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public static class Mapping
{
    public static UserResponse ToResponse(this User u) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.Role, u.Phone, u.ProfileImageUrl, u.CreatedAt);

    public static CategoryResponse ToResponse(this Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.CategoryType, c.MinimumAge, c.MaximumAge, c.DistanceKm, c.EntryFee);

    public static EventDetailResponse ToDetail(this Event e) =>
        new(e.EventId, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType, e.BannerImageUrl,
            e.OrganiserId, $"{e.Organiser.FirstName} {e.Organiser.LastName}",
            e.Categories.OrderBy(c => c.Name).Select(c => c.ToResponse()).ToList());

    public static EnrolmentResponse ToResponse(this Enrolment e) =>
        new(e.EnrolmentId, e.ParticipantId, $"{e.Participant.FirstName} {e.Participant.LastName}",
            e.EventId, e.Event.Name, e.Event.EventDate, e.CategoryId, e.Category.Name, e.Status, e.EnrolledAt);

    public static ResultResponse ToResponse(this Result r, int totalFinishers) =>
        new(r.ResultId, r.EnrolmentId, r.Enrolment.EventId, r.Enrolment.Event.Name, r.Enrolment.Event.EventDate,
            r.Enrolment.Category.Name, $"{r.Enrolment.Participant.FirstName} {r.Enrolment.Participant.LastName}",
            r.FinishTime, r.Position, totalFinishers);
}
