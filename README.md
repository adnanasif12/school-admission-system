# School Admission System

Online school admission on a first-come, first-served basis. A student applies for a seat in a class
(each shift is its own class, e.g. "Class 6 - Morning Shift"). The seat is **held** for a limited time;
paying the fee **confirms** the admission, otherwise the seat goes back to the pool.

- Backend: .NET 8 Web API + PostgreSQL (EF Core) in [backend/](backend/)
- Frontend: React 18 + Vite in `frontend/` (added in phase 2)
- Everything runs with Docker only; you do not need .NET or Node installed.

## Run the app

```bash
docker compose up --build
```

- Backend API: <http://localhost:8080> (health check: <http://localhost:8080/health>)
- Data lives in the named volume `pgdata`, so it survives `docker compose down` and `docker compose up`.
  (`docker compose down -v` deletes it.)

Configuration (environment variables on the `backend` service in [docker-compose.yml](docker-compose.yml)):

| Variable | Default | Meaning |
|---|---|---|
| `ConnectionStrings__Default` | set in compose | PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | `600` | How long a seat hold lasts. Override: `HOLD_DURATION_SECONDS=30 docker compose up` |
| `EXPIRY_SWEEP_INTERVAL_SECONDS` | `5` | How often the background service marks lapsed holds `EXPIRED` |

## Run the backend tests (Docker only)

One command. It starts the compose Postgres, then runs the whole xUnit suite in a
`mcr.microsoft.com/dotnet/sdk:8.0` container:

```bash
docker compose run --rm backend-tests
```

- The test service is in the `tests` compose profile, so a plain `docker compose up` never starts it.
- Tests create and drop their own throw-away databases (`sa_test_*`) on the same Postgres server,
  so the real `school_admission` data is never touched.
- The first run downloads NuGet packages (a couple of minutes); later runs take well under a minute.
- The suite includes the concurrency tests: 60 parallel hold requests on a 5-seat class must yield
  exactly 5 successes, and the same with two separate backend instances sharing one database.
