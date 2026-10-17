using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using RaceDay.Api.Controllers;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Tests.Support;

namespace RaceDay.Api.Tests;

/// <summary>
/// Two layers: (1) the SessionAuthorize filter itself returns 401/403 correctly,
/// (2) every protected action is actually decorated with the right role, so the filter runs in the real pipeline.
/// </summary>
public class RoleEnforcementTests
{
    private static AuthorizationFilterContext ContextFor(int? userId, string? role)
    {
        var actionContext = new ActionContext(TestHost.NewHttpContext(userId, role), new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static int? Run(SessionAuthorizeAttribute filter, int? userId, string? role)
    {
        var context = ContextFor(userId, role);
        filter.OnAuthorization(context);
        return (context.Result as IStatusCodeActionResult)?.StatusCode;
    }

    [Fact]
    public void Anonymous_OnProtectedEndpoint_Returns401()
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, Run(new SessionAuthorizeAttribute(Roles.Organiser), null, null));
    }

    [Fact]
    public void Anonymous_OnAnySignedInEndpoint_Returns401()
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, Run(new SessionAuthorizeAttribute(), null, null));
    }

    [Fact]
    public void Participant_OnOrganiserEndpoint_Returns403()
    {
        Assert.Equal(StatusCodes.Status403Forbidden, Run(new SessionAuthorizeAttribute(Roles.Organiser), 3, Roles.Participant));
    }

    [Fact]
    public void Organiser_OnParticipantEndpoint_Returns403()
    {
        Assert.Equal(StatusCodes.Status403Forbidden, Run(new SessionAuthorizeAttribute(Roles.Participant), 1, Roles.Organiser));
    }

    [Fact]
    public void TamperedRoleInSession_Returns403()
    {
        Assert.Equal(StatusCodes.Status403Forbidden, Run(new SessionAuthorizeAttribute(Roles.Organiser), 1, "organiser"));
    }

    [Theory]
    [InlineData(Roles.Organiser)]
    [InlineData(Roles.Participant)]
    public void MatchingRole_IsAllowedThrough(string role)
    {
        Assert.Null(Run(new SessionAuthorizeAttribute(role), 1, role));
    }

    [Theory]
    [InlineData(Roles.Organiser)]
    [InlineData(Roles.Participant)]
    public void AnySignedInUser_IsAllowedOnRoleFreeEndpoint(string role)
    {
        Assert.Null(Run(new SessionAuthorizeAttribute(), 1, role));
    }

    [Theory]
    [InlineData(typeof(EventsController), nameof(EventsController.Create), Roles.Organiser)]
    [InlineData(typeof(EventsController), nameof(EventsController.Update), Roles.Organiser)]
    [InlineData(typeof(EventsController), nameof(EventsController.Delete), Roles.Organiser)]
    [InlineData(typeof(EventsController), nameof(EventsController.UploadBanner), Roles.Organiser)]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.Create), Roles.Organiser)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.ForEvent), Roles.Organiser)]
    [InlineData(typeof(ResultsController), nameof(ResultsController.Capture), Roles.Organiser)]
    [InlineData(typeof(ResultsController), nameof(ResultsController.Update), Roles.Organiser)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.Enrol), Roles.Participant)]
    [InlineData(typeof(EnrolmentsController), nameof(EnrolmentsController.Mine), Roles.Participant)]
    [InlineData(typeof(ResultsController), nameof(ResultsController.Mine), Roles.Participant)]
    public void ProtectedAction_DeclaresExactlyTheExpectedRole(Type controller, string action, string expectedRole)
    {
        var attribute = controller.GetMethod(action)!.GetCustomAttribute<SessionAuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(new[] { expectedRole }, attribute!.Roles);
    }

    [Theory]
    [InlineData(typeof(ProfileController))]
    public void SignedInOnlyController_IsProtectedAtClassLevel(Type controller)
    {
        var attribute = controller.GetCustomAttribute<SessionAuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Empty(attribute!.Roles);
    }

    [Theory]
    [InlineData(typeof(EventsController), nameof(EventsController.GetAll))]
    [InlineData(typeof(EventsController), nameof(EventsController.GetById))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.GetAll))]
    [InlineData(typeof(AuthController), nameof(AuthController.Register))]
    [InlineData(typeof(AuthController), nameof(AuthController.Login))]
    public void PublicAction_HasNoSessionRequirement(Type controller, string action)
    {
        Assert.Null(controller.GetMethod(action)!.GetCustomAttribute<SessionAuthorizeAttribute>());
        Assert.Null(controller.GetCustomAttribute<SessionAuthorizeAttribute>());
    }
}
