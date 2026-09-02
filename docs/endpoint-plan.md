# RaceDay API — Endpoint Plan

Part 1, Section B. Covers Authentication, User Profile, Events, Categories, Event Enrolments and Results as required, plus a small number of additional endpoints identified as necessary to support the functional requirements (marked *(additional)* below). This plan will be implemented as-is in Part 2; any deviation will be explained in the README.

Endpoints are grouped into three tables — **Organiser**, **Participant**, and **Shared/Public** — rather than strictly two, because several endpoints (viewing your own profile, browsing events and categories, logging in) are used identically by both roles and don't belong to either one exclusively. This grouping is a documentation choice for readability only: the system is still one unified API, and the Part 2 implementation will be organised by resource (an `EventsController`, an `EnrolmentsController`, etc.) rather than by role — a single controller can, and will, have some actions open to Organisers and others to Participants.

**Role Required key:** `None` = public, no token needed · `Any` = any authenticated user · `Organiser` / `Participant` = that role only.

## Organiser Endpoints

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register/organiser | Registers a new Organiser account. | None | `{ fullName, email, password }` | 201 Created – new organiser id; 400 Bad Request – invalid data; 409 Conflict – email already registered |
| POST | /api/events | Creates a new event owned by the logged-in organiser. | Organiser | `{ name, description, date, location, distanceKm, eventType }` | 201 Created – new event; 400 Bad Request – invalid data |
| PUT | /api/events/{id} | Updates an event owned by the logged-in organiser. | Organiser | `{ name, description, date, location, distanceKm, eventType }` | 200 OK – updated event; 403 Forbidden – not the owning organiser; 404 Not Found |
| DELETE | /api/events/{id} | Deletes an event owned by the logged-in organiser. | Organiser | None | 204 No Content; 403 Forbidden; 404 Not Found |
| POST | /api/events/{eventId}/categories | Adds a new category to an event owned by the organiser. | Organiser | `{ name, description }` | 201 Created – new category; 403 Forbidden; 404 Not Found |
| PUT | /api/categories/{id} | Updates a category belonging to an event owned by the organiser. | Organiser | `{ name, description }` | 200 OK – updated category; 403 Forbidden; 404 Not Found |
| DELETE | /api/categories/{id} | Deletes a category from an event owned by the organiser. | Organiser | None | 204 No Content; 403 Forbidden; 404 Not Found |
| GET | /api/events/{eventId}/enrolments | Lists all enrolments for an event owned by the organiser. | Organiser | None | 200 OK – array of enrolments; 403 Forbidden; 404 Not Found |
| POST | /api/enrolments/{id}/results | Captures a finish time and position for a participant's enrolment. | Organiser | `{ finishTime, position }` | 201 Created – new result; 403 Forbidden – not the owning organiser; 404 Not Found; 409 Conflict – result already captured |
| PUT | /api/results/{id} | *(additional)* Updates a previously captured result. | Organiser | `{ finishTime, position }` | 200 OK – updated result; 403 Forbidden; 404 Not Found |
| GET | /api/events/{eventId}/results | *(additional)* Lists all results for an event owned by the organiser. | Organiser | None | 200 OK – array of results; 403 Forbidden; 404 Not Found |

## Participant Endpoints

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register/participant | Registers a new Participant account. | None | `{ fullName, email, password }` | 201 Created – new participant id; 400 Bad Request – invalid data; 409 Conflict – email already registered |
| POST | /api/enrolments | Enters the logged-in participant into an event by selecting a category. Records the link between participant, event and category. | Participant | `{ eventId, categoryId }` | 201 Created – new enrolment; 404 Not Found – event/category does not exist; 409 Conflict – already enrolled in this event |
| GET | /api/enrolments/me | Lists the logged-in participant's own enrolments. | Participant | None | 200 OK – array of enrolments |
| DELETE | /api/enrolments/{id} | *(additional)* Cancels the logged-in participant's own enrolment. | Participant | None | 204 No Content; 403 Forbidden – not the owning participant; 404 Not Found |
| GET | /api/results/me | Lists the logged-in participant's own results. | Participant | None | 200 OK – array of results |

## Shared / Public Endpoints

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/login | Authenticates a user by checking both Organisers and Participants for a matching email, and issues an access token. | None | `{ email, password }` | 200 OK – token and role; 401 Unauthorized – invalid credentials |
| GET | /api/users/me | Retrieves the logged-in user's own profile, resolved server-side to the Organiser or Participant record matching the token. | Any | None | 200 OK – profile details; 401 Unauthorized |
| PUT | /api/users/me | Updates the logged-in user's own profile information, in whichever table the token resolves to. | Any | `{ fullName, phone, email }` | 200 OK – updated profile; 400 Bad Request – invalid data |
| GET | /api/events | Lists all events. | Any | None | 200 OK – array of events |
| GET | /api/events/{id} | Retrieves details for a specific event. | Any | None | 200 OK – event details; 404 Not Found |
| GET | /api/events/{eventId}/categories | Lists all categories defined for a specific event. | Any | None | 200 OK – array of categories; 404 Not Found – event does not exist |
