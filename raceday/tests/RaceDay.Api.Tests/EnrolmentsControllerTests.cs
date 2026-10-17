using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Api.Tests.Support;

namespace RaceDay.Api.Tests;

public class EnrolmentsControllerTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    [Fact]
    public async Task Enrol_ValidEventAndCategory_Returns201AndRecordsParticipantEventAndCategory()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);

        var result = await controller.Enrol(new EnrolmentRequest { EventId = ids.UpcomingEventId, CategoryId = ids.UpcomingCategoryId });

        Assert.Equal(StatusCodes.Status201Created, TestHost.StatusOf(result));
        var stored = await db.Enrolments.SingleAsync(e => e.EventId == ids.UpcomingEventId);
        Assert.Equal(ids.AidenParticipantId, stored.ParticipantId);
        Assert.Equal(ids.UpcomingCategoryId, stored.CategoryId);
        Assert.Equal(EnrolmentStatuses.Pending, stored.Status);

        var body = TestHost.BodyOf<EnrolmentResponse>(result);
        Assert.Equal("Half 21K", body.CategoryName);
        Assert.Equal("Aiden Jacobs", body.ParticipantName);
    }

    [Fact]
    public async Task Enrol_SameEventTwice_Returns409AndKeepsOneRecord()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);
        var request = new EnrolmentRequest { EventId = ids.UpcomingEventId, CategoryId = ids.UpcomingCategoryId };
        await controller.Enrol(request);

        var second = await controller.Enrol(request);

        Assert.Equal(StatusCodes.Status409Conflict, TestHost.StatusOf(second));
        Assert.Equal(1, await db.Enrolments.CountAsync(e => e.EventId == ids.UpcomingEventId));
    }

    [Fact]
    public async Task Enrol_CategoryFromADifferentEvent_Returns400()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);

        var result = await controller.Enrol(new EnrolmentRequest { EventId = ids.UpcomingEventId, CategoryId = ids.LebosCategoryId });

        Assert.Equal(StatusCodes.Status400BadRequest, TestHost.StatusOf(result));
        Assert.False(await db.Enrolments.AnyAsync(e => e.EventId == ids.UpcomingEventId));
    }

    [Fact]
    public async Task Enrol_EventThatAlreadyHappened_Returns400()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var newcomer = new User { FirstName = "Lwazi", LastName = "Zulu", Email = "lwazi@raceday.example", Role = Roles.Participant, PasswordHash = _hasher.Hash("Pass1234") };
        db.Users.Add(newcomer);
        await db.SaveChangesAsync();
        var controller = new EnrolmentsController(db).SignedInAs(newcomer.UserId, Roles.Participant);

        var result = await controller.Enrol(new EnrolmentRequest { EventId = ids.PastEventId, CategoryId = ids.PastCategoryId });

        Assert.Equal(StatusCodes.Status400BadRequest, TestHost.StatusOf(result));
        Assert.False(await db.Enrolments.AnyAsync(e => e.ParticipantId == newcomer.UserId));
    }

    [Fact]
    public async Task Enrol_UnknownEvent_Returns404()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);

        var result = await controller.Enrol(new EnrolmentRequest { EventId = 9999, CategoryId = ids.UpcomingCategoryId });

        Assert.Equal(StatusCodes.Status404NotFound, TestHost.StatusOf(result));
    }

    [Fact]
    public async Task Enrol_UnknownCategory_Returns404()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);

        var result = await controller.Enrol(new EnrolmentRequest { EventId = ids.UpcomingEventId, CategoryId = 9999 });

        Assert.Equal(StatusCodes.Status404NotFound, TestHost.StatusOf(result));
    }

    [Fact]
    public async Task Mine_ReturnsOnlyTheSignedInParticipantsEnrolments()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);
        await controller.Enrol(new EnrolmentRequest { EventId = ids.UpcomingEventId, CategoryId = ids.UpcomingCategoryId });

        var mine = TestHost.BodyOf<IEnumerable<EnrolmentResponse>>(await controller.Mine()).ToList();

        Assert.Equal(2, mine.Count);
        Assert.All(mine, e => Assert.Equal(ids.AidenParticipantId, e.ParticipantId));
    }

    [Fact]
    public async Task ForEvent_OwningOrganiser_SeesEveryEntrantWithCategoryAndStatus()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var entrants = TestHost.BodyOf<IEnumerable<EnrolmentResponse>>(await controller.ForEvent(ids.PastEventId)).ToList();

        Assert.Equal(2, entrants.Count);
        Assert.All(entrants, e => Assert.Equal("Senior 10K", e.CategoryName));
        Assert.All(entrants, e => Assert.Equal(EnrolmentStatuses.Confirmed, e.Status));
    }

    [Fact]
    public async Task ForEvent_AnotherOrganisersEvent_Returns403()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EnrolmentsController(db).SignedInAs(ids.LeboOrganiserId, Roles.Organiser);

        Assert.Equal(StatusCodes.Status403Forbidden, TestHost.StatusOf(await controller.ForEvent(ids.PastEventId)));
    }
}
