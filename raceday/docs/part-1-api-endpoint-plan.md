# RaceDay API endpoint plan from Part 1

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Create an account with a validated role. | None | `firstName, lastName, email, password, role, phone` | 201; 400 validation; 409 duplicate email |
| POST | `/api/auth/login` | Verify credentials and create a server session. | None | `email, password` | 200; 401 invalid credentials |
| POST | `/api/auth/logout` | Clear the current session. | Any signed in | None | 204; 401 |
| GET | `/api/profile` | Read the signed-in profile. | Any signed in | None | 200; 401 |
| PUT | `/api/profile` | Update the signed-in profile. | Any signed in | `firstName, lastName, phone, profileImageUrl` | 200; 400; 401 |
| GET | `/api/events` | List upcoming events with filters. | None | Query string | 200 array |
| GET | `/api/events/{id}` | Return event details and categories. | None | None | 200; 404 |
| POST | `/api/events` | Create an owned event. | Organiser | `name, description, eventDate, location, distanceKm, eventType` | 201; 400; 401; 403 |
| PUT | `/api/events/{id}` | Update an owned event. | Organiser | Event fields | 200; 400; 401; 403; 404 |
| DELETE | `/api/events/{id}` | Delete an owned event. | Organiser | None | 204; 401; 403; 404; 409 |
| GET | `/api/events/{id}/categories` | List event categories. | None | None | 200; 404 |
| POST | `/api/events/{id}/categories` | Create an event category. | Organiser | `name, categoryType, age range, distanceKm, entryFee` | 201; 400; 403; 404; 409 |
| POST | `/api/enrolments` | Enrol the current participant in a category. | Participant | `eventId, categoryId` | 201; 400; 401; 403; 404; 409 |
| GET | `/api/enrolments/mine` | View personal enrolments. | Participant | None | 200; 401; 403 |
| GET | `/api/events/{id}/enrolments` | View entrants for an owned event. | Organiser | None | 200; 401; 403; 404 |
| POST | `/api/results` | Capture a finish time and position. | Organiser | `enrolmentId, finishTime, position` | 201; 400; 403; 404; 409 |
| GET | `/api/results/mine` | View personal race history. | Participant | None | 200; 401; 403 |
| PUT | `/api/results/{id}` | Correct an owned event result. | Organiser | `finishTime, position` | 200; 400; 403; 404; 409 |
| POST | `/api/events/{id}/banner` | Upload an event banner through the storage adapter. | Organiser | multipart `file` | 200 URL; 400; 403; 404; 413 |
