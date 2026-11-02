> Архів матеріалів моноліту до виділення Auth/Catalog. Актуальний запуск, архітектура та перевірки: [кореневий README](../README.md) і [docs/lab-verification.md](../docs/lab-verification.md).

# CinemaCity final documentation

## Purpose

CinemaCity is a movie and series catalog with authentication, user profiles, ratings, reviews, public/private lists, TMDb import, admin moderation and Docker-based local launch.

The latest implementation focused on removing demo-only behavior, making the backend cleaner through repositories and interfaces, fixing episode/TMDb issues, improving public profile workflows, adding tests, seed data and Docker verification.

## Solution structure

- `backend/CinemaCity.Domain` contains domain entities and enums: `Movie`, `Season`, `Episode`, `User`, `Rating`, `Review`, `UserMovieList`, `WatchlistItem`, `Person`, `Genre`, `Country`, `Language`.
- `backend/CinemaCity.Application` contains contracts used by the API and infrastructure, especially persistence and import interfaces.
- `backend/CinemaCity.Infrastructure` contains EF Core, PostgreSQL persistence, repositories and TMDb integration.
- `backend/CinemaCity.API` contains Minimal API endpoint modules, auth setup, JWT token creation, seed data and app startup.
- `backend/CinemaCity.Tests` contains backend integration tests with a real isolated PostgreSQL test database.
- `frontend` contains the React/Vite UI, admin panel, auth forms, profile pages, catalog pages and frontend tests.

## Backend architecture

The API is split into endpoint modules instead of keeping all routes in `Program.cs`:

- `AuthEndpoints` handles registration, login, refresh, logout, current user and Google auth.
- `MovieEndpoints` handles catalog browsing, movie details and admin movie CRUD.
- `SeriesEndpoints` handles seasons, episodes and episode description editing.
- `RatingEndpoints` handles movie and episode ratings.
- `ReviewEndpoints` handles reviews, comments, likes and moderation.
- `ProfileEndpoints` handles current user profile, watchlist, custom lists, public profiles and public reviews.
- `HomeEndpoints` handles recommendations and popular-now data.
- `SearchEndpoints` handles global search across movies, series, episodes and people.
- `AdminEndpoints` handles genres, persons and uploads.
- `AdminImportEndpoints` handles TMDb import actions.

`Program.cs` only wires services, middleware, endpoint modules, Swagger, CORS, static files and database startup.

## Repository and interface layer

Persistence access was moved behind interfaces in `CinemaCity.Application`.

Main interfaces:

- `IRepository<TEntity>` provides generic querying and mutation methods:
  - `Query()`
  - `Local`
  - `Add`
  - `AddRange`
  - `Remove`
  - `RemoveRange`
- `IUnitOfWork` wraps saving and transactions:
  - `SaveChangesAsync`
  - `BeginTransactionAsync`
- `IUserRepository` contains auth-specific user lookups:
  - username/email existence checks
  - lookup by email
  - lookup by Google id or email
  - user creation
- `IRefreshTokenRepository` contains refresh-token lookup and creation.
- `IDatabaseManager` abstracts migrations and database health checks.
- `ITmdbImportService` abstracts TMDb import use cases.

Infrastructure implementations:

- `EfRepository<TEntity>` implements `IRepository<TEntity>` through EF Core `DbSet`.
- `UnitOfWork` implements `IUnitOfWork` through `AppDbContext`.
- `UserRepository` implements `IUserRepository`.
- `RefreshTokenRepository` implements `IRefreshTokenRepository`.
- `DatabaseManager` implements `IDatabaseManager`.
- `TmdbImportService` implements `ITmdbImportService`.

Dependency registration is done in `backend/CinemaCity.Infrastructure/DependencyInjection.cs`.

## Authentication and sessions

The demo-token behavior was removed from the user-facing flow.

Authentication now uses normal JWT-based authorization:

- registration creates a real user;
- login verifies password through `IPasswordHasher<User>`;
- access token is returned to the frontend;
- refresh token is stored server-side as a hash;
- refresh token is also kept in an HTTP-only cookie;
- `/api/auth/refresh` restores the session after page reload;
- `/api/auth/logout` revokes refresh tokens and clears the cookie;
- `/api/auth/me` returns the current user from JWT.

Important backend files:

- `backend/CinemaCity.API/Auth/AuthEndpoints.cs`
- `backend/CinemaCity.API/Auth/AuthSetup.cs`
- `backend/CinemaCity.API/Auth/TokenService.cs`
- `backend/CinemaCity.API/Auth/JwtOptions.cs`
- `backend/CinemaCity.Domain/Entities/RefreshToken.cs`
- `backend/CinemaCity.Application/Persistence/IRefreshTokenRepository.cs`
- `backend/CinemaCity.Infrastructure/Persistence/RefreshTokenRepository.cs`

Frontend session behavior:

- access token is kept in app state;
- refresh is called on initial app load;
- user stays logged in after page reload while refresh token is valid;
- auth forms no longer show technical/demo messages;
- password fields have an eye button to show/hide password.

## TMDb import

TMDb import is handled by `ITmdbImportService` and `TmdbImportService`.

Supported import flows:

- popular movies;
- popular TV shows;
- one movie by TMDb id.

Imported content is stored as normal local domain data:

- movies and series are stored in `Movie`;
- seasons are stored in `Season`;
- episodes are stored in `Episode`;
- genres, countries, languages and persons are connected through normal relations.

Episode import was fixed so TV show import brings seasons and episodes into the local database instead of only creating the series card.

TMDb-related files:

- `backend/CinemaCity.Application/Imports/ITmdbImportService.cs`
- `backend/CinemaCity.Infrastructure/Tmdb/TmdbClient.cs`
- `backend/CinemaCity.Infrastructure/Tmdb/TmdbDtos.cs`
- `backend/CinemaCity.Infrastructure/Tmdb/TmdbImportService.cs`
- `backend/CinemaCity.Infrastructure/Tmdb/NullableDateOnlyJsonConverter.cs`
- `backend/CinemaCity.API/Admin/AdminImportEndpoints.cs`

## TMDb descriptions

TMDb descriptions are imported into the normal `Description` fields. There is no separate Ukrainian-description field.

Import behavior:

- movie and series titles/metadata are loaded with the default `en-US` TMDb data;
- movie and series descriptions first try the Ukrainian TMDb translation (`uk-UA` / `uk`);
- if a Ukrainian overview is missing, the importer falls back to the English overview;
- season and episode names stay from the default data;
- season and episode descriptions are read from Ukrainian season details when available;
- if Ukrainian season/episode overview is missing, the importer falls back to the English overview.

This keeps the domain model simple: `Movie.Description`, `Season.Description` and `Episode.Description` are the only description fields.

## Episodes and ratings

Episode statistics and user rating behavior were corrected.

Current behavior:

- episode rating updates the episode average and count;
- if a user has rated an episode, the API returns that user's episode rating;
- duplicate episode rating returns conflict instead of creating a second rating;
- when a user rates an episode, the parent series is moved to `Watched`;
- the UI shows the user's existing rating instead of encouraging repeated rating.

Important files:

- `backend/CinemaCity.API/Ratings/RatingEndpoints.cs`
- `backend/CinemaCity.API/Series/SeriesEndpoints.cs`
- `frontend/src/main.tsx`

## Reviews and comments

Review and comment behavior was cleaned up:

- public user profile shows approved reviews;
- private or pending data is hidden from public profile pages;
- comment moderation uses deletion instead of rejection where requested;
- users and admins can manage allowed review/comment actions from the UI.

Important files:

- `backend/CinemaCity.API/Reviews/ReviewEndpoints.cs`
- `backend/CinemaCity.API/Profile/ProfileEndpoints.cs`
- `frontend/src/main.tsx`

## Public profiles and lists

Users can open other users' public profiles.

Public profile includes:

- username and public counters;
- approved reviews left by that user;
- public custom lists;
- list items inside public lists.

Private lists are not returned from public endpoints and are not rendered in the UI.

Current user lists:

- users can create custom lists;
- lists can be public or private;
- movies and series can be added to custom lists;
- public lists are visible on public profile pages;
- private lists stay available only to their owner.

Important files:

- `backend/CinemaCity.API/Profile/ProfileEndpoints.cs`
- `frontend/src/main.tsx`

## Admin panel

The admin panel supports:

- movie and series creation/editing;
- genre management;
- person management;
- poster/photo uploads;
- cast and crew assignment;
- seasons and episodes management;
- TMDb import;
- review moderation;
- Ukrainian description editing for imported TMDb content.

Frontend admin code is currently concentrated in `frontend/src/main.tsx`.

## Frontend UI updates

Implemented UI improvements:

- password visibility toggle with a standard eye button;
- cleaned auth messages so logout/register/login does not show technical inline notices;
- public profile review viewing;
- public list browsing;
- improved logo/branding;
- admin validation for non-negative cast sort order;
- episode rating display fixes.

Reusable password component:

- `frontend/src/components/PasswordField.tsx`
- `frontend/src/components/PasswordField.test.tsx`

## Seed data

Development seed data is handled by `backend/CinemaCity.API/DevelopmentDataSeeder.cs`.

It creates:

- 10 development users;
- ratings;
- approved reviews;
- one private list per user;
- one public list per user;
- list items.

Seed users:

- email: `dev.user1@cinemacity.local` through `dev.user10@cinemacity.local`;
- password: `DevUser123!`;
- role: regular user.

The seeder is idempotent. If development users already exist, it skips creating duplicates.

## Testing

Backend tests are in `backend/CinemaCity.Tests`.

They use:

- `WebApplicationFactory<Program>`;
- isolated PostgreSQL test database on `localhost:55432`;
- temporary database name per test run;
- real API requests through `HttpClient`;
- test seed data in `CinemaCityApiFactory`.

Covered backend scenarios:

- registration, login, JWT session and `/api/auth/me`;
- public profile lists and approved reviews;
- episode rating, duplicate rating conflict and watched-list update;
- admin validation for negative cast sort order;
- core admin and user API scenarios.

Frontend tests:

- `PasswordField` renders a password input;
- eye button toggles password visibility;
- input change callback works.

Run all main checks:

```powershell
docker compose --profile test up -d postgres-test
dotnet test CinemaCity.slnx

cd frontend
npm test
npm run build
```

## Docker

The project has Docker support for PostgreSQL, API and frontend.

Files:

- `docker-compose.yml`
- `.dockerignore`
- `backend/CinemaCity.API/Dockerfile`
- `frontend/Dockerfile`
- `frontend/nginx.conf`

Default launch:

```powershell
docker compose up -d --build
```

Default URLs:

- frontend: `http://localhost:5173`;
- API: `http://localhost:5029`;
- PostgreSQL: `localhost:5432`.

If `5173` is already busy:

```powershell
$env:FRONTEND_PORT = "5174"
docker compose up -d --build
```

Then frontend is available at `http://localhost:5174`.

Useful commands:

```powershell
docker compose logs -f api
docker compose logs -f frontend
docker compose down
```

For TMDb import inside Docker, set `TMDB_API_TOKEN` before starting the stack.

## Docker Desktop

After the first CLI launch, Docker Desktop shows the Compose app as `cinemacity`.

From Docker Desktop, the user can start or stop:

- `cinemacity-postgres`;
- `cinemacity-api`;
- `cinemacity-frontend`;
- `cinemacity-postgres-test` when test profile was started.

The first launch should still be done from the project folder so Docker Compose can build the images and create the app.

## Verification status

Latest verified checks:

- `dotnet test CinemaCity.slnx` passed.
- `npm test` passed.
- `npm run build` passed.
- `docker compose build api frontend` passed.
- Docker API health endpoint returned PostgreSQL available.
- Docker frontend returned the built HTML page.

## Notes for future work

- Frontend code is still concentrated in `frontend/src/main.tsx`; splitting it into feature modules would make the app easier to maintain.
- npm audit currently reports dependency vulnerabilities from the frontend dependency tree. They were not auto-fixed to avoid uncontrolled dependency churn.
- `Documentation/` is ignored by Git, so final project documentation should stay in tracked root-level files unless `.gitignore` is changed.
