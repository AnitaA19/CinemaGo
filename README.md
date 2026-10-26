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

Send the JWT as `Authorization: Bearer <token>`. New users are registered as `Customer`; promote a user to `Admin` directly in the database.

## Behavior notes

- Errors are mapped centrally (`GlobalExceptionHandler`) to ProblemDetails:
  `NotFoundException`→404, `ConflictException`→409, `ValidationException`→400, `UnauthorizedAccessException`→401.
- DTOs use data annotations (`[Required]`, `[EmailAddress]`, `[Range]`, ...); invalid models return 400 automatically.
- Session start times are UTC: values must carry a `Z`/offset and be in the future.
- Known limitation: the session overlap check is read-then-write, so two simultaneous requests could both pass it.
  A production fix would use a serializable transaction or a database-level constraint.