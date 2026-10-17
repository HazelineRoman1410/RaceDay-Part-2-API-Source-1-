using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Security;

namespace RaceDay.Api.Tests.Support;

public static class TestHost
{
    /// <summary>A brand-new, isolated in-memory database per test.</summary>
    public static RaceDayDbContext NewDb() =>
        new(new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase($"raceday-tests-{Guid.NewGuid():N}")
            .Options);

    /// <summary>An HttpContext whose session is either empty (anonymous) or signed in with a user ID and role.</summary>
    public static DefaultHttpContext NewHttpContext(int? userId = null, string? role = null)
    {
        var context = new DefaultHttpContext();
        var feature = new TestSessionFeature();
        context.Features.Set<ISessionFeature>(feature);

        if (userId is int id)
        {
            feature.Session.SetInt32(SessionKeys.UserId, id);
            feature.Session.SetString(SessionKeys.Role, role ?? string.Empty);
        }
        return context;
    }

    public static T SignedInAs<T>(this T controller, int userId, string role) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = NewHttpContext(userId, role) };
        return controller;
    }

    public static T Anonymous<T>(this T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = NewHttpContext() };
        return controller;
    }

    public static int? StatusOf(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

    public static T BodyOf<T>(IActionResult result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result).Value);
}
