# School Admission System

Online school admission on a first-come, first-served basis. A parent applies for a seat in a class
(every shift is its own class, e.g. "Class 6 - Morning Shift"). The seat is **held** for a limited time;
paying the admission fee **confirms** it, otherwise the seat goes back to the pool automatically.

- **Backend:** .NET 8 Web API + PostgreSQL (EF Core) in [backend/](backend/)
- **Frontend:** React 18 + Vite + TypeScript, served by nginx, in [frontend/](frontend/)
- **Everything runs in Docker.** You do not need .NET, Node or PostgreSQL installed.

## Prerequisites

Only **Docker** with Compose v2 (Docker Desktop on Windows/macOS, or Docker Engine + the compose plugin on Linux).

## Start

```bash
docker compose up --build
```

| What | URL |
|---|---|
| **Web app (open this)** | <http://localhost:3000> |
| Backend API | <http://localhost:8080> (health: <http://localhost:8080/health>) |

The first build downloads base images and packages and takes a few minutes. Data is kept in the named
volume `pgdata`, so it survives `docker compose down` and `docker compose up`.
Stop with `docker compose down`; `docker compose down -v` also deletes the data.

### Using the app

1. Type the student's 17-digit birth registration number at the top (it is remembered on this device).
2. **Create class (admin)** tab: give a name and a number of seats.
3. **Classes** tab: see total / on hold / confirmed / available seats (refreshes every few seconds) and press **Apply**.
4. **My applications** tab: a live countdown. Press **Pay fee** before it ends, or **Cancel**. At zero the
   application turns `EXPIRED` by itself and the seat is released.

### Port 3000 is busy?

Pick another host port with `FRONTEND_PORT` and open that port instead.

```bash
# bash / Git Bash / macOS / Linux
FRONTEND_PORT=3100 docker compose up --build
```

```powershell
# PowerShell
$env:FRONTEND_PORT = "3100"; docker compose up --build
```

Or put `FRONTEND_PORT=3100` in a file named `.env` next to `docker-compose.yml` (it is git-ignored).
Then open <http://localhost:3100>. The same works for `HOLD_DURATION_SECONDS` and `API_URL`.

> Tip for Git Bash on Windows: set variables with `export NAME=value` first. An inline prefix such as
> `NAME=value docker compose up` did not reach the containers in my tests.

## Configuration

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `HOLD_DURATION_SECONDS` | backend | `600` | How long a seat hold lasts. Example: `30` to watch an expiry quickly. |
| `ConnectionStrings__Default` | backend | set in compose | PostgreSQL connection string (the backend reads its DB only from the environment). |
| `EXPIRY_SWEEP_INTERVAL_SECONDS` | backend | `5` | How often the background service marks lapsed holds `EXPIRED`. |
| `API_URL` | frontend | `http://localhost:8080` | Backend URL **as the browser sees it**. Written into `/config.js` when the frontend container starts, so it can be changed without rebuilding. |
| `FRONTEND_PORT` | compose | `3000` | Host port of the web app. |

## Tests (Docker only)

Every command below needs nothing but Docker. Each starts the containers it needs; none of them touches
the data you created in the app (tests use their own throw-away databases or their own classes).

| What | Command | Typical time |
|---|---|---|
| Backend tests (xUnit, 17 tests, incl. concurrency) | `docker compose run --rm backend-tests` | ~1 min (first run ~3 min: NuGet restore) |
| Frontend unit tests + type-check (Vitest, 27 tests) | `docker compose run --rm frontend-tests` | ~30 s |
| Frontend end-to-end in a real browser (Playwright, 30 checks) | `docker compose run --rm frontend-e2e` | ~2 min (first run downloads a ~2 GB browser image) |
| Two-backend proof on real containers | `docker compose --profile scale run --rm two-backend-proof` | ~30 s |

These services live in compose profiles (`tests`, `e2e`, `scale`), so a plain `docker compose up` never starts them.

### Backend tests

`docker compose run --rm backend-tests` runs the xUnit suite against the compose PostgreSQL. The main
concurrency test sends 60 parallel hold requests at a 5-seat class and requires **exactly 5** successes
(`ConcurrencyTests.ManyParallelHolds_OnFewSeats_ExactlyTotalSeatsSucceed`). Other tests cover expiry
without any API call, idempotent confirm, ownership (403/404), the one-active-hold rule and the JSON contract.

### Frontend end-to-end test

`docker compose run --rm frontend-e2e` drives headless Chromium through the whole flow: create a class with
2 seats, apply from two students, a third gets the "no seats" message, one pays, one cancels, one is left to
expire (the countdown flips to `EXPIRED` with no reload and Pay is disabled), and the "server unreachable"
message. It uses its own backend with a 30-second hold, so the real app's settings do not matter.
Screenshots are written to `frontend/e2e/shots/` (git-ignored).

### Two-backend proof (bonus)

`held + confirmed` must stay at or below `totalSeats` even with two backend **processes** sharing one database.

```bash
docker compose --profile scale run --rm two-backend-proof
```

This starts a second backend on <http://localhost:8081> (same PostgreSQL as the one on 8080) and, for 6 classes
with 1-10 seats, fires 150 parallel apply requests each, alternating between the two backends. It checks the seat
arithmetic through both backends, pays for half of the holds, tries to over-book again, and exits non-zero on any
violation. Result of my run:

```text
Backends: http://backend:8080  |  http://backend2:8080
Both healthy. 6 rounds x 150 parallel requests, alternating between the backends.

round  seats  requests  201 (b1/b2)   409  other  held+confirmed  available   verdict
    1      1       150  1 (1/0)       149      0               1          0   OK
    2      2       150  2 (1/1)       148      0               2          0   OK
    3      3       150  3 (2/1)       147      0               3          0   OK
    4      5       150  5 (3/2)       145      0               5          0   OK
    5      7       150  7 (4/3)       143      0               7          0   OK
    6     10       150  10 (5/5)      140      0              10          0   OK

PASS: held + confirmed never exceeded totalSeats across 900 concurrent requests on 2 backends sharing 1 database.
```

Sanity check that the proof can fail: with the `FOR UPDATE` line removed from the backend, the same script
reported `VIOLATION` in every round (for example 20 holds accepted on a 1-seat class and 121 on a 10-seat class)
and exited with code 1. The line was restored afterwards.

How it is guaranteed: see "Concurrency" in [ASSUMPTIONS.md](ASSUMPTIONS.md) (row lock with `SELECT ... FOR UPDATE`
plus a partial unique index).

You can also try both backends by hand after `docker compose --profile scale up -d`:
<http://localhost:8080/classes> and <http://localhost:8081/classes> show the same data.

## API (contract from the brief)

Base URL `http://localhost:8080`, JSON in and out. The student is identified by the `X-Student-Id` header (17 digits).

| # | Method and path | Success | Notes |
|---|---|---|---|
| 1 | `POST /classes` | 201 `{id,name,totalSeats}` | 400 on invalid name or seats |
| 2 | `GET /classes` | 200 list with `heldSeats`, `confirmedSeats`, `availableSeats` | |
| 3 | `GET /classes/{classId}` | 200 | 404 |
| 4 | `POST /classes/{classId}/holds` | 201 `{holdId,classId,studentId,status,expiresAt}` | 409 no seats or already applied; 400 bad ID; 404 |
| 5 | `GET /holds/{holdId}` | 200 | status `HELD`/`CONFIRMED`/`EXPIRED`/`CANCELLED`; 404 |
| 6 | `POST /holds/{holdId}/confirm` | 200 `{holdId,status}` | 403 not yours; 404; 409 expired or cancelled; repeat is 200 |
| 7 | `DELETE /holds/{holdId}` | 204 | 403; 404; 409 if already paid |
| 8 | `GET /me/holds` | 200 list with `className` | |

Errors always carry a JSON body `{ "error": "CODE", "message": "plain-language text" }`.
`bash scripts/verify-api.sh` walks the main flow with curl against a running stack and prints every status and body.

## Project structure

```text
docker-compose.yml          db + backend + frontend (+ optional test/proof services under profiles)
backend/
  Dockerfile
  src/SchoolAdmission.Api/
    Api/                    endpoints, DTOs, error handling
    Services/               ClassService, HoldService (locking), HoldExpiryService (background sweeper)
    Data/                   EF Core DbContext, migrations, start-up migrator
    Domain/                 entities and statuses
  tests/SchoolAdmission.Tests/   xUnit tests (throw-away database per test class)
frontend/
  Dockerfile, nginx.conf, docker-entrypoint.sh   nginx + runtime config.js generator
  src/                      React app (api client, components, hooks, time/error helpers)
  src/__tests__/            Vitest unit tests
  e2e/                      Playwright end-to-end test
scripts/
  verify-api.sh             curl walkthrough of the API
  two-backend-proof.mjs     two-backend concurrency proof
ASSUMPTIONS.md              decisions the brief left open, with reasons
AI_USAGE.md                 how AI was used and which mistakes were caught
```
