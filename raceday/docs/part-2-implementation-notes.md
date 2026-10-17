# RaceDay Part 2: plan vs implementation

Every route in `docs/part-1-api-endpoint-plan.md` is implemented. This file records each deliberate deviation so the marker can see it was a decision, not drift.

| Area | Part 1 plan | Part 2 implementation | Why |
|---|---|---|---|
| Database creation | Hand-written `part-1-raceday-schema.sql` | EF Core code-first (`RaceDayDbContext`) with the same table names, column sizes, PK/FK/UNIQUE/CHECK names and defaults | Brief requires code-first. The SQL script stays as the reference spec. |
| Seed passwords | `REPLACE_WITH_API_HASH` placeholders | `DbSeeder` hashes `RaceDay@2026` with PBKDF2 at startup | A real hash can only be produced by the API's hasher. |
| `GET/PUT /api/profile` body | `name, phone, profileImageUrl` | `firstName, lastName, phone, profileImageUrl` | Matches the separate `FirstName`/`LastName` columns in the ERD. Email and role are read-only. |
| `POST /api/enrolments` status | Default `Pending` | Saved as `Pending` | Same as SQL default; organiser confirmation is a Part 3 UI action. |
| `POST /api/events/{id}/banner` | Upload to Blob Storage | Same route and contract, stored through `IBannerStorage` (local disk in Part 2) | Azure Blob is a Part 3 requirement; Part 3 swaps the DI registration only. |
| Results ownership/rules | 201/400/403/404/409 | Same, plus 409 when a position is already used in that event | Two finishers cannot both be 1st. |
| WeatherSnapshots, RoutePoints | Tables only | Tables and seed data only, no endpoints | Used by the Part 3 race-day prep page. |

## Role matrix (enforced by `[SessionAuthorize]`)

| Endpoint | Public | Any signed in | Organiser | Participant |
|---|---|---|---|---|
| POST /api/auth/register, /login | ✓ | | | |
| POST /api/auth/logout | | ✓ | | |
| GET/PUT /api/profile | | ✓ | | |
| GET /api/events, /api/events/{id}, /api/events/{id}/categories | ✓ | | | |
| POST/PUT/DELETE /api/events, POST categories, POST banner | | | ✓ (own events) | |
| GET /api/events/{id}/enrolments | | | ✓ (own events) | |
| POST /api/results, PUT /api/results/{id} | | | ✓ (own events) | |
| POST /api/enrolments, GET /api/enrolments/mine, GET /api/results/mine | | | | ✓ |
