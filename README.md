RaceDay
RaceDay is an event management platform for South African road running, walking and cycling events. Organisers publish events, define age or distance categories, see who entered and capture official results. Participants browse upcoming events, enter a category and track their personal race history.

This branch (part-2) contains the RESTful API built in ASP.NET Core 8 with Entity Framework Core (code-first), server-side session authentication, Swagger and xUnit tests running in GitHub Actions.

Roles
Role	What they can do
Organiser	Create, edit and delete their own events; add categories; upload an event banner; view everyone enrolled in their events; capture and correct finish times and positions.
Participant	Register and log in; browse events and categories; enter an event by choosing a category; view their own enrolments; view their own results ("47th of 312").
Both roles can view and update their own profile. Role is chosen at registration and is whitelisted to these two values. A Participant calling an Organiser endpoint gets 403, and vice versa; anyone not logged in gets 401.

Project structure
RaceDay.sln
src/RaceDay.Api/
  Controllers/   Auth, Profile, Events, Categories, Enrolments, Results
  Data/          RaceDayDbContext (code-first model = Part 1 schema), DbSeeder
  Dtos/          Request/response contracts + validation, mapping
  Models/        User, Event, Category, Enrolment, Result, WeatherSnapshot, RoutePoint
  Security/      SessionAuthorizeAttribute, session keys/helpers
  Services/      PBKDF2 password hasher, banner storage adapter
tests/RaceDay.Api.Tests/   xUnit tests (EF Core InMemory, fake session)
docs/                      Part 1 ERD, endpoint plan, SQL + Part 2 implementation notes
.github/workflows/         part-1-ci.yml, part-2-ci.yml
Setup and run
Prerequisites: .NET 8 SDK, SQL Server LocalDB (installed with Visual Studio) or any SQL Server instance, Visual Studio 2022 or dotnet CLI.

Clone and switch to the Part 2 branch
git clone <your-repo-url> && cd <repo>
git checkout part-2
Check the connection string in src/RaceDay.Api/appsettings.json (ConnectionStrings:RaceDayDb). The default targets (localdb)\MSSQLLocalDB.
Create the database with EF Core migrations
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/RaceDay.Api
dotnet ef database update --project src/RaceDay.Api
(If you skip this, the API creates the schema on first run with EnsureCreated.)
Run the API
dotnet run --project src/RaceDay.Api --launch-profile RaceDay.Api
Open https://localhost:7180/swagger. Call POST /api/auth/login first; the session cookie then authenticates every other request in Swagger.
Demo accounts (seeded on first run)
Password for all: RaceDay@2026

Email	Role
thandi.organiser@raceday.example	Organiser (Golden Hour 10K, Umgeni River Walk)
lebo.organiser@raceday.example	Organiser (Cape Peninsula Cycle Classic)
aiden.participant@raceday.example	Participant
nandi.participant@raceday.example	Participant
Authentication design
POST /api/auth/login verifies the PBKDF2 hash and writes UserId and Role into the ASP.NET Core server-side session. The browser only holds the opaque, HttpOnly .RaceDay.Session cookie.
[SessionAuthorize] (an IAuthorizationFilter) runs before every protected action: no session → 401, wrong role → 403.
Ownership is checked inside the action: an Organiser can only change events they created (403 otherwise).
Passwords are hashed with salted PBKDF2-SHA256 (100 000 iterations) and compared in constant time. Plain text is never stored or returned.
Endpoints
All endpoints from docs/part-1-api-endpoint-plan.md are implemented. Deliberate differences are listed in docs/part-2-implementation-notes.md.

Tests
dotnet test
Covers registration and login (success and failure), password hashing, event management, cross-role rejection (401/403) for every protected action, ownership rules, enrolments being recorded with the right participant/event/category, categories, profile and results.

CI/CD
.github/workflows/part-2-ci.yml validates the repo structure, restores, builds and runs all tests on every push.

Evidence placeholder: add the actual green GitHub Actions screenshot as docs/evidence/part-2-ci-green.png after the workflow runs.

Video
Unlisted YouTube walkthrough:https://youtu.be/NTgtbJ2FZIY

References
Microsoft (2024a) Session and state management in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state (Accessed: 16 October 2026).

Microsoft (2024b) Filters in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters (Accessed: 16 October 2026).

Microsoft (2024c) Get started with Swashbuckle and ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle (Accessed: 16 October 2026).

Microsoft (2024d) Testing EF Core applications. Available at: https://learn.microsoft.com/en-us/ef/core/testing/ (Accessed: 16 October 2026).

Microsoft (2024e) Rfc2898DeriveBytes.Pbkdf2 method. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2 (Accessed: 16 October 2026).

OWASP Foundation (2024) Password Storage Cheat Sheet. Available at: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html (Accessed: 16 October 2026).
