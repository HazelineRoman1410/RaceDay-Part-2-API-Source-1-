using RaceDay.Api.Models;

namespace RaceDay.Api.Security;

public static class SessionKeys
{
    public const string UserId = "RaceDay.UserId";
    public const string Role = "RaceDay.Role";
    public const string DisplayName = "RaceDay.DisplayName";
    public const string CookieName = ".RaceDay.Session";
}

/// <summary>Typed helpers over the server-side session so controllers never touch raw keys.</summary>
public static class RaceDaySessionExtensions
{
    public static void SignIn(this ISession session, User user)
    {
        session.Clear();
        session.SetInt32(SessionKeys.UserId, user.UserId);
        session.SetString(SessionKeys.Role, user.Role);
        session.SetString(SessionKeys.DisplayName, $"{user.FirstName} {user.LastName}");
    }

    public static int? GetUserId(this ISession session) => session.GetInt32(SessionKeys.UserId);

    public static string? GetRole(this ISession session) => session.GetString(SessionKeys.Role);
}
