using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;
using RaceDay.Api.Tests.Support;

namespace RaceDay.Api.Tests;

public class AuthControllerTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    private static RegisterRequest ValidRegistration(string role = Roles.Participant) => new()
    {
        FirstName = "Sipho",
        LastName = "Khumalo",
        Email = "  Sipho.Khumalo@RaceDay.example ",
        Password = "StrongPass#1",
        Role = role,
        Phone = "0825550199"
    };

    [Theory]
    [InlineData(Roles.Participant)]
    [InlineData(Roles.Organiser)]
    public async Task Register_ValidDetailsForEitherRole_Returns201AndStoresHashedPassword(string role)
    {
        using var db = TestHost.NewDb();
        var controller = new AuthController(db, _hasher).Anonymous();

        var result = await controller.Register(ValidRegistration(role));

        Assert.Equal(StatusCodes.Status201Created, TestHost.StatusOf(result));
        var body = TestHost.BodyOf<UserResponse>(result);
        Assert.Equal(role, body.Role);
        Assert.Equal("sipho.khumalo@raceday.example", body.Email);

        var stored = await db.Users.SingleAsync();
        Assert.NotEqual("StrongPass#1", stored.PasswordHash);
        Assert.True(_hasher.Verify("StrongPass#1", stored.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateEmailWithDifferentCasing_Returns409()
    {
        using var db = TestHost.NewDb();
        TestData.Seed(db, _hasher);
        var controller = new AuthController(db, _hasher).Anonymous();
        var request = ValidRegistration();
        request.Email = "THANDI@raceday.example";

        var result = await controller.Register(request);

        Assert.Equal(StatusCodes.Status409Conflict, TestHost.StatusOf(result));
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "thandi@raceday.example"));
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400AndCreatesNothing()
    {
        using var db = TestHost.NewDb();
        var controller = new AuthController(db, _hasher).Anonymous();

        var result = await controller.Register(ValidRegistration(role: "Admin"));

        Assert.Equal(StatusCodes.Status400BadRequest, TestHost.StatusOf(result));
        Assert.False(await db.Users.AnyAsync());
    }

    [Theory]
    [InlineData("short1", "Participant")]
    [InlineData("onlyletters", "Participant")]
    [InlineData("StrongPass#1", "Admin")]
    public void RegisterRequest_WeakPasswordOrUnknownRole_FailsModelValidation(string password, string role)
    {
        var request = ValidRegistration();
        request.Password = password;
        request.Role = role;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndStoresUserIdAndRoleInSession()
    {
        using var db = TestHost.NewDb();
        var ids = TestData.Seed(db, _hasher);
        var controller = new AuthController(db, _hasher).Anonymous();

        var result = await controller.Login(new LoginRequest { Email = "Thandi@RaceDay.example", Password = TestData.Password });

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(result));
        var session = controller.HttpContext.Session;
        Assert.Equal(ids.ThandiOrganiserId, session.GetUserId());
        Assert.Equal(Roles.Organiser, session.GetRole());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401AndLeavesSessionEmpty()
    {
        using var db = TestHost.NewDb();
        TestData.Seed(db, _hasher);
        var controller = new AuthController(db, _hasher).Anonymous();

        var result = await controller.Login(new LoginRequest { Email = "aiden@raceday.example", Password = "WrongPass#9" });

        Assert.Equal(StatusCodes.Status401Unauthorized, TestHost.StatusOf(result));
        Assert.Null(controller.HttpContext.Session.GetUserId());
        Assert.Null(controller.HttpContext.Session.GetRole());
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        using var db = TestHost.NewDb();
        TestData.Seed(db, _hasher);
        var controller = new AuthController(db, _hasher).Anonymous();

        var result = await controller.Login(new LoginRequest { Email = "nobody@raceday.example", Password = TestData.Password });

        Assert.Equal(StatusCodes.Status401Unauthorized, TestHost.StatusOf(result));
    }

    [Fact]
    public async Task RegisterThenLogin_NewParticipant_CanSignInWithTheSamePassword()
    {
        using var db = TestHost.NewDb();
        var controller = new AuthController(db, _hasher).Anonymous();
        await controller.Register(ValidRegistration());

        var login = await controller.Login(new LoginRequest { Email = "sipho.khumalo@raceday.example", Password = "StrongPass#1" });

        Assert.Equal(StatusCodes.Status200OK, TestHost.StatusOf(login));
        Assert.Equal(Roles.Participant, controller.HttpContext.Session.GetRole());
    }

    [Fact]
    public void Logout_SignedIn_Returns204AndClearsSession()
    {
        using var db = TestHost.NewDb();
        var controller = new AuthController(db, _hasher).SignedInAs(7, Roles.Participant);

        var result = controller.Logout();

        Assert.Equal(StatusCodes.Status204NoContent, TestHost.StatusOf(result));
        Assert.Null(controller.HttpContext.Session.GetUserId());
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
