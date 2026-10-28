# CinemaGo

ASP.NET Core Web API (.NET 10) for a cinema: movies and showtimes (sessions).
Layered solution: `Domain` (entities, DTOs, exceptions) → `Application` (services) → `Infrastructure` (EF Core / SQL Server) → `Web` (controllers).

## Running

1. Set the SQL Server connection string (`ConnectionStrings:DefaultConnection`) and the `Jwt` section
   (`Key` ≥ 32 bytes, `Issuer`, `Audience`, optional `ExpiresInMinutes`) via `appsettings.Development.json` or user secrets.
2. Apply migrations: `dotnet ef database update --project CinemaGo.Infrastructure --startup-project CinemaGo.Web`
3. `dotnet run --project CinemaGo.Web` and open `/swagger`.
4. Tests: `dotnet test`

## API

| Method | Route | Access |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | anonymous |
| GET | `/api/movies`, `/api/movies/{id}`, `/api/sessions`, `/api/sessions/{id}` | anonymous |
| POST/PUT/DELETE | `/api/movies`, `/api/sessions` | **Admin** |
| POST | `/api/bookings` | JWT (any user) |
| GET | `/api/bookings/my` | JWT (own bookings) |
| GET | `/api/bookings/{id}` | JWT (owner) or **Admin** |
| DELETE | `/api/bookings/{id}` | JWT (owner) or **Admin** |
| GET | `/api/bookings` | **Admin** |

Send the JWT as `Authorization: Bearer <token>`. New users are registered as `Customer`; promote a user to `Admin` directly in the database.

## Bookings

All booking endpoints require a valid JWT (`Authorization: Bearer <token>`); anonymous calls return 401.
The user is taken from the token, never from the request body. Another user's booking is reported as 404 (non-admin).

`POST /api/bookings`

```json
{ "sessionId": 1, "seatId": 3 }
```

`201 Created` (with a `Location` header pointing to `/api/bookings/{id}`):

```json
{
  "id": 15,
  "userId": 7,
  "sessionId": 1,
  "seatId": 3,
  "movieTitle": "Dune",
  "sessionStartsAt": "2026-11-01T18:00:00Z",
  "rowNumber": 4,
  "seatNumber": 5,
  "isVip": true,
  "status": "Active",
  "createdAt": "2026-10-28T12:50:00Z",
  "ticketCode": "3f9c2a7e5b1d4c0f8a6e2d9b7c4a1e50",
  "finalPrice": 20.00
}
```

The price is calculated on the server (`vipPrice` for VIP seats, otherwise `standardPrice`).

| Status | Reason |
|---|---|
| 400 | seat is in another hall, or the session is cancelled / already started, or the booking is already cancelled |
| 403 | cancelling another user's booking |
| 404 | session, seat or booking not found |
| 409 | seat already booked for this session |

`DELETE /api/bookings/{id}` cancels the booking (204) and frees the seat for a new booking.
Double booking is prevented both by a service check and by a unique database index on active
`(SessionId, SeatId)`, which also protects against simultaneous requests.

## Behavior notes

- Errors are mapped centrally (`GlobalExceptionHandler`) to ProblemDetails:
  `NotFoundException`→404, `ConflictException`→409, `ValidationException`→400, `UnauthorizedAccessException`→401.
- DTOs use data annotations (`[Required]`, `[EmailAddress]`, `[Range]`, ...); invalid models return 400 automatically.
- Session start times are UTC: values must carry a `Z`/offset and be in the future.
- Known limitation: the session overlap check is read-then-write, so two simultaneous requests could both pass it.
  A production fix would use a serializable transaction or a database-level constraint.