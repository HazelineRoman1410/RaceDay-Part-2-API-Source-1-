using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Api.Tests.Support;

namespace RaceDay.Api.Tests;

public class CategoriesAndProfileTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    private static CategoryRequest Under20() => new()
    {
        Name = "Under 20", CategoryType = CategoryTypes.Age, MinimumAge = 12, MaximumAge = 19, DistanceKm = 21.1m, EntryFee = 120
    };

    [Fact]
    public async Task CreateCategory_OnOwnEvent_Returns201()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new CategoriesController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        var result = await controller.Create(ids.UpcomingEventId, Under20());

        Assert.Equal(StatusCodes.Status201Created, TestHost.StatusOf(result));
        Assert.Equal(ids.UpcomingEventId, TestHost.BodyOf<CategoryResponse>(result).EventId);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameOnSameEvent_Returns409()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new CategoriesController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);
        var request = Under20();
        request.Name = "Half 21K";

        Assert.Equal(StatusCodes.Status409Conflict, TestHost.StatusOf(await controller.Create(ids.UpcomingEventId, request)));
    }

    [Fact]
    public async Task CreateCategory_OnAnotherOrganisersEvent_Returns403()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new CategoriesController(db).SignedInAs(ids.ThandiOrganiserId, Roles.Organiser);

        Assert.Equal(StatusCodes.Status403Forbidden, TestHost.StatusOf(await controller.Create(ids.LebosEventId, Under20())));
    }

    [Fact]
    public void CategoryRequest_InvertedAgeRange_FailsValidation()
    {
        var request = Under20();
        request.MinimumAge = 40;
        request.MaximumAge = 20;

        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("MinimumAge"));
    }

    [Fact]
    public async Task GetCategories_Anonymous_Returns200ForExistingEventAnd404Otherwise()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new CategoriesController(db).Anonymous();

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(await controller.GetAll(ids.UpcomingEventId)));
        Assert.Equal(StatusCodes.Status404NotFound, TestHost.StatusOf(await controller.GetAll(9999)));
    }

    [Theory]
    [InlineData(Roles.Organiser)]
    [InlineData(Roles.Participant)]
    public async Task Profile_BothRoles_CanReadAndUpdateTheirOwnProfile(string role)
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var userId = role == Roles.Organiser ? ids.ThandiOrganiserId : ids.AidenParticipantId;
        var controller = new ProfileController(db).SignedInAs(userId, role);

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(await controller.Get()));

        var update = await controller.Update(new UpdateProfileRequest { FirstName = "Updated", LastName = "Name", Phone = "0711234567" });

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(update));
        var stored = (await db.Users.FindAsync(userId))!;
        Assert.Equal("Updated", stored.FirstName);
        Assert.Equal(role, stored.Role);
    }

    [Fact]
    public async Task Profile_Anonymous_Returns401()
    {
        using var db = TestHost.NewDb();
        var controller = new ProfileController(db).Anonymous();

        Assert.Equal(StatusCodes.Status401Unauthorized, TestHost.StatusOf(await controller.Get()));
    }
}
