# School Admission System

Online school admission on a first-come, first-served basis. A student applies for a seat in a class
(each shift is its own class, e.g. "Class 6 - Morning Shift"). The seat is **held** for a limited time;
paying the fee **confirms** the admission, otherwise the seat goes back to the pool.

- Backend: .NET 8 Web API + PostgreSQL (EF Core) in [backend/](backend/)
- Frontend: React 18 + Vite + TypeScript in [frontend/](frontend/), served by nginx
- Everything runs with Docker only; you do not need .NET or Node installed.

## Run the app

```bash
docker compose up --build
```

- **Open the app: <http://localhost:3000>**
- Backend API: <http://localhost:8080> (health check: <http://localhost:8080/health>)
- If port 3000 is already used on your machine, pick another one:
  `FRONTEND_PORT=3100 docker compose up --build` and open <http://localhost:3100>.
- Data lives in the named volume `pgdata`, so it survives `docker compose down` and `docker compose up`.
  (`docker compose down -v` deletes it.)

Configuration (environment variables on the `backend` service in [docker-compose.yml](docker-compose.yml)):

| Variable | Default | Meaning |
|---|---|---|
| `ConnectionStrings__Default` | set in compose | PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | `600` | How long a seat hold lasts, e.g. `export HOLD_DURATION_SECONDS=30` then `docker compose up -d` (use `export`; an inline `VAR=x docker compose` prefix did not reach the containers in Git Bash on Windows) |
| `EXPIRY_SWEEP_INTERVAL_SECONDS` | `5` | How often the background service marks lapsed holds `EXPIRED` |
| `API_URL` (frontend service) | `http://localhost:8080` | Backend URL as seen by the **browser**. Written to `/config.js` when the frontend container starts, so it can change without rebuilding the image |

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

## Manual API check

With the stack running, `bash scripts/verify-api.sh` walks the main flow with curl (create class, holds, 409s, double confirm, cancel, /me/holds) and prints every status code and JSON body.
