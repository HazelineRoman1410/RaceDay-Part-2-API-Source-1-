# Part 2 demo script

1. Start the API and open `/swagger`.
2. `POST /api/auth/login` as `thandi.organiser@raceday.example` with `RaceDay@2026`.
3. Call `GET /api/events` and `GET /api/events/{id}`.
4. Call `POST /api/events` with a future event. Then call `POST /api/events/{id}/categories`.
5. Log out. Log in as `aiden.participant@raceday.example`.
6. Call `GET /api/events`, then `POST /api/enrolments` using an event and category from the response.
7. Demonstrate that the Participant calling `POST /api/events` gets 403.
8. Log back in as Thandi and show `GET /api/events/{id}/enrolments`.
9. Use a seeded past event to call `POST /api/results`, then switch to Aiden and show `GET /api/results/mine`.
10. Run `dotnet test` and open the green GitHub Actions run.

Use a human voiceover and explain the student's own decisions. Do not submit an AI-generated voice.
