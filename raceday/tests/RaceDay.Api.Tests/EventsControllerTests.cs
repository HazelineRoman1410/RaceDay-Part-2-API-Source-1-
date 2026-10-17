using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Api.Tests.Support;

namespace RaceDay.Api.Tests;

public class EventsControllerTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    private sealed class FakeBannerStorage : IBannerStorage
    {
        public int Saves { get; private set; }
        public Task<string> SaveBannerAsync(int eventId, Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.FromResult($"https://storage.test/banners/event-{eventId}{extension}");
        }
    }

    private static EventRequest NewEvent(string name = "Durban Beachfront 5K") => new()
    {
        Name = name,
        Description = "Flat promenade route from uShaka to the Moses Mabhida stadium.",
        EventDate = DateTime.UtcNow.AddDays(60),
        Location = "Durban",
        DistanceKm = 5,
        EventType = EventTypes.Run
    };

    private static IFormFile Image(string contentType, int bytes) =>
        new FormFile(new MemoryStream(new byte[bytes]), 0, bytes, "file", "banner.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    [Fact]
    public async Task Create_AsOrganiser_Returns201AndOwnerComesFromSession()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Create(NewEvent());

        Assert.Equal(StatusCodes.Status201Created, TestHost.StatusOf(result));
        var saved = await db.Events.SingleAsync(e => e.Name == "Durban Beachfront 5K");
        Assert.Equal(ids.ThandiOrganiserId, saved.OrganiserId);
        Assert.Equal(EventTypes.Run, saved.EventType);
    }

    [Fact]
    public async Task Create_WithPastDate_Returns400()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        var request = NewEvent();
        request.EventDate = DateTime.UtcNow.AddDays(-1);

        var result = await controller.Create(request);

        Assert.Equal(StatusCodes.Status400BadRequest, TestHost.StatusOf(result));
    }

    [Fact]
    public async Task Update_OwnEvent_Returns200AndPersistsChanges()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        var request = NewEvent("Soweto Sunrise 21K (new route)");
        request.DistanceKm = 21.1m;

        var result = await controller.Update(ids.UpcomingEventId, request);

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(result));
        Assert.Equal("Soweto Sunrise 21K (new route)", (await db.Events.FindAsync(ids.UpcomingEventId))!.Name);
    }

    [Fact]
    public async Task Update_AnotherOrganisersEvent_Returns403AndChangesNothing()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Update(ids.LebosEventId, NewEvent("Hijacked"));

        Assert.Equal(StatusCodes.Status403Forbidden, TestHost.StatusOf(result));
        Assert.Equal("Cape Peninsula Cycle Classic", (await db.Events.FindAsync(ids.LebosEventId))!.Name);
    }

    [Fact]
    public async Task Update_MissingEvent_Returns404()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        Assert.Equal(StatusCodes.Status404NotFound, TestHost.StatusOf(await controller.Update(9999, NewEvent())));
    }

    [Fact]
    public async Task Delete_OwnEventWithoutEnrolments_Returns204AndRemovesEventAndCategories()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Delete(ids.UpcomingEventId);

        Assert.Equal(StatusCodes.Status204NoContent, TestHost.StatusOf(result));
        Assert.False(await db.Events.AnyAsync(e => e.EventId == ids.UpcomingEventId));
        Assert.False(await db.Categories.AnyAsync(c => c.EventId == ids.UpcomingEventId));
    }

    [Fact]
    public async Task Delete_EventWithEnrolments_Returns409()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Delete(ids.PastEventId);

        Assert.Equal(StatusCodes.Status409Conflict, TestHost.StatusOf(result));
        Assert.True(await db.Events.AnyAsync(e => e.EventId == ids.PastEventId));
    }

    [Fact]
    public async Task Delete_AnotherOrganisersEvent_Returns403()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        Assert.Equal(StatusCodes.Status403Forbidden, TestHost.StatusOf(await controller.Delete(ids.LebosEventId)));
    }

    [Fact]
    public async Task GetAll_Anonymous_ReturnsOnlyUpcomingEventsInDateOrder()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).Anonymous();

        var events = TestHost.BodyOf<IEnumerable<EventSummaryResponse>>(await controller.GetAll(new EventQuery())).ToList();

        Assert.Equal(new[] { ids.UpcomingEventId, ids.LebosEventId }, events.Select(e => e.EventId));
    }

    [Fact]
    public async Task GetAll_FilterByTypeAndLocation_ReturnsMatchesOnly()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).Anonymous();

        var events = TestHost.BodyOf<IEnumerable<EventSummaryResponse>>(
            await controller.GetAll(new EventQuery { Type = EventTypes.Cycle, Location = "cape" })).ToList();

        Assert.Single(events);
        Assert.Equal(ids.LebosEventId, events[0].EventId);
    }

    [Fact]
    public async Task GetById_ExistingEvent_Returns200WithCategories()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new EventsController(db, new FakeBannerStorage()).Anonymous();

        var detail = TestHost.BodyOf<EventDetailResponse>(await controller.GetById(ids.UpcomingEventId));

        Assert.Equal("Thandi Mokoena", detail.OrganiserName);
        Assert.Contains(detail.Categories, c => c.Name == "Half 21K");
    }

    [Fact]
    public async Task GetById_MissingEvent_Returns404()
    {
        using var db = TestHost.NewDb();
        var controller = new EventsController(db, new FakeBannerStorage()).Anonymous();

        Assert.Equal(StatusCodes.Status404NotFound, TestHost.StatusOf(await controller.GetById(404)));
    }

    [Fact]
    public async Task UploadBanner_ValidPng_Returns200AndSavesUrlOnEvent()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var storage = new FakeBannerStorage();
        var controller = new EventsController(db, storage).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(ids.UpcomingEventId, new BannerUploadRequest { File = Image("image/png", 1024) }, default);

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(result));
        Assert.Equal(1, storage.Saves);
        Assert.EndsWith(".png", (await db.Events.FindAsync(ids.UpcomingEventId))!.BannerImageUrl);
    }

    [Theory]
    [InlineData("application/pdf", 1024, StatusCodes.Status400BadRequest)]
    [InlineData("image/png", 0, StatusCodes.Status400BadRequest)]
    [InlineData("image/jpeg", (int)EventsController.MaxBannerBytes + 1, StatusCodes.Status413PayloadTooLarge)]
    public async Task UploadBanner_BadFile_IsRejectedWithoutStoring(string contentType, int bytes, int expectedStatus)
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var storage = new FakeBannerStorage();
        var controller = new EventsController(db, storage).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(ids.UpcomingEventId, new BannerUploadRequest { File = Image(contentType, bytes) }, default);

        Assert.Equal(expectedStatus, TestHost.StatusOf(result));
        Assert.Equal(0, storage.Saves);
    }
}
