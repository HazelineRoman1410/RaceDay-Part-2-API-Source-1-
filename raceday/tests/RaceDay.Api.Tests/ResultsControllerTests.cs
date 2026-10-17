using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Api.Tests.Support;

namespace RaceDay.Api.Tests;

public class ResultsControllerTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    [Fact]
    public async Task Capture_OwnPastEvent_Returns201AndStoresTimeAndPosition()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new ResultsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Capture(new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 48, 22), Position = 47 });

        Assert.Equal(StatusCodes.Status201Created, TestHost.StatusOf(result));
        var stored = await db.Results.SingleAsync();
        Assert.Equal(new TimeSpan(0, 48, 22), stored.FinishTime);
        Assert.Equal(47, stored.Position);
    }

    [Fact]
    public async Task Capture_SecondResultForSameEnrolment_Returns409()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new ResultsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        await controller.Capture(new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 48, 22), Position = 47 });

        var again = await controller.Capture(new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 50, 0), Position = 50 });

        Assert.Equal(StatusCodes.Status409Conflict, TestHost.StatusOf(again));
    }

    [Fact]
    public async Task Capture_PositionAlreadyTakenInEvent_Returns409()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new ResultsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        await controller.Capture(new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 48, 22), Position = 1 });

        var clash = await controller.Capture(new ResultRequest { EnrolmentId = ids.NandiPastEnrolmentId, FinishTime = new TimeSpan(0, 52, 10), Position = 1 });

        Assert.Equal(StatusCodes.Status409Conflict, TestHost.StatusOf(clash));
    }

    [Fact]
    public async Task Capture_OnAnotherOrganisersEvent_Returns403()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new ResultsController(db).SignedInAs(ids.LeboOrganiserId, Roles.Organiser);

        var result = await controller.Capture(new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 48, 22), Position = 47 });

        Assert.Equal(StatusCodes.Status403Forbidden, TestHost.StatusOf(result));
        Assert.False(await db.Results.AnyAsync());
    }

    [Fact]
    public async Task Capture_UnknownEnrolment_Returns404()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new ResultsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Capture(new ResultRequest { EnrolmentId = 9999, FinishTime = new TimeSpan(0, 48, 22), Position = 47 });

        Assert.Equal(StatusCodes.Status404NotFound, TestHost.StatusOf(result));
    }

    [Fact]
    public async Task Mine_Participant_SeesOwnResultWithTotalFinishers()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var organiser = new ResultsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        await organiser.Capture(new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 48, 22), Position = 1 });
        await organiser.Capture(new ResultRequest { EnrolmentId = ids.NandiPastEnrolmentId, FinishTime = new TimeSpan(0, 52, 10), Position = 2 });

        var participant = new ResultsController(db).SignedInAs(ids.AidenParticipantId, Roles.Participant);
        var mine = TestHost.BodyOf<IEnumerable<ResultResponse>>(await participant.Mine()).ToList();

        var only = Assert.Single(mine);
        Assert.Equal("Golden Hour 10K", only.EventName);
        Assert.Equal(1, only.Position);
        Assert.Equal(2, only.TotalFinishers);
    }

    [Fact]
    public async Task Update_OwnEventResult_Returns200WithCorrectedValues()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new ResultsController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        var created = TestHost.BodyOf<ResultResponse>(await controller.Capture(
            new ResultRequest { EnrolmentId = ids.AidenPastEnrolmentId, FinishTime = new TimeSpan(0, 48, 22), Position = 47 }));

        var updated = await controller.Update(created.ResultId, new ResultUpdateRequest { FinishTime = new TimeSpan(0, 48, 5), Position = 46 });

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(updated));
        Assert.Equal(46, (await db.Results.FindAsync(created.ResultId))!.Position);
    }
}
