> Архів матеріалів моноліту до виділення Auth/Catalog. Актуальний запуск, архітектура та перевірки: [кореневий README](../README.md) і [docs/lab-verification.md](../docs/lab-verification.md).

# CinemaCity testing and local verification

Full project summary is available in `FINAL_DOCUMENTATION.md`.

## Backend API tests

Backend tests live in `backend/CinemaCity.Tests` and cover the main API flows:

- registration, login, JWT session and `/api/auth/me`;
- public user profile data, including public lists and approved reviews only;
- episode rating, duplicate rating protection and watched-list update;
- admin validation for movie cast data.

Run them with an isolated PostgreSQL test container:

```powershell
docker compose --profile test up -d postgres-test
dotnet test CinemaCity.slnx
```

The test project creates a temporary database on `localhost:55432` for every run and removes it after the tests finish.

## Frontend tests

Frontend tests currently cover the reusable password field, including the eye button for showing and hiding the password.

```powershell
cd frontend
npm test
npm run build
```

## Development seed data

The API can seed local development users, ratings, reviews and public/private lists through `DevelopmentDataSeeder`.

Seed users use this pattern:

- email: `dev.user1@cinemacity.local` through `dev.user10@cinemacity.local`;
- password: `DevUser123!`;
- role: regular user;
- each user gets ratings, approved reviews, one private list and one public list.

The seeder is idempotent: if these development users already exist, it skips creating duplicates.

## Docker Compose

The full local stack is available through Docker Compose:

```powershell
docker compose up -d --build
```

Services:

- PostgreSQL: `localhost:5432`;
- API: `http://localhost:5029`;
- frontend: `http://localhost:5173`.

If `5173` is already used by a local Vite server, run the frontend container on another port:

```powershell
$env:FRONTEND_PORT = "5174"
docker compose up -d --build api frontend
```

For TMDb imports inside Docker, provide `TMDB_API_TOKEN` in the shell or `.env` before starting the stack.
