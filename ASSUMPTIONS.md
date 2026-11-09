# Assumptions

The brief leaves some things open. This is what I decided, and why. Each line is a deliberate choice, not an accident.

## Identity and access

- **Student ID is exactly 17 digits (`[0-9]{17}`), sent in `X-Student-Id`.** The brief says "17-digit birth registration number"; I do not guess at any inner structure (year, district...).
- **No login and no admin authentication.** The brief says "no login" and that the school admin also works from the UI. Anyone can create a class, and anyone who types an ID acts as that student. Fine for this exercise, not for production.
- **Missing or malformed `X-Student-Id` gives 400, not 401.** There is no authentication scheme, so "401 unauthenticated" would be misleading; the request is simply invalid.
- **Confirming or cancelling someone else's application gives 403; an unknown id gives 404.** The two cases are different problems and the UI can explain each in plain words.
- **`GET /holds/{id}` does not require the header** (the brief's example shows none). If the header *is* sent and belongs to someone else, it returns 403. Consequence: anyone who knows a hold id can read it. Ids are random 64-bit values, so they cannot be guessed, but this is not access control.
- **Ids look like `c_<16 hex>` and `h_<16 hex>`.** The brief only says the format is my choice; random ids avoid leaking how many applications exist.

## Applications (holds)

- **A student can apply again after cancelling or after an expiry.** Only `HELD` (not yet expired) and `CONFIRMED` count as "active". The brief limits *active* applications per class, so finished ones must not block a retry.
- **A student may hold seats in different classes at once** (for example both shifts of Class 6). The brief limits one active application *per class*, not overall.
- **A `CONFIRMED` application cannot be cancelled: `DELETE` returns 409.** The fee is already paid and refunds are not in the brief. Cancelling an already cancelled or expired application returns 204, so retries are harmless.
- **Confirm only works on your own, non-expired `HELD` application.** Confirming twice returns 200 again and `confirmedSeats` rises once. Confirming an expired application returns 409, a cancelled one 409.
- **"Pay fee" is simulated.** As the brief says, pressing the button counts as a successful payment; there is no payment gateway.
- **`HOLD_DURATION_SECONDS` default is 600 (10 minutes), as in the brief.** A missing, non-numeric or non-positive value falls back to 600 instead of crashing at start-up.
- **`GET /holds/{id}` and `/me/holds` also return extra fields** (`className`, `studentId`, ...). The brief allows additions but not removals or renames.

## Seats and expiry

- **Only non-expired `HELD` rows count as held.** Every count query ignores a `HELD` row whose `expiresAt` has passed, so a seat returns at the exact moment the hold lapses. This is the "lazy" part and it needs no API call and no background job.
- **A background service also marks lapsed holds `EXPIRED`** (every 5 s, `EXPIRY_SWEEP_INTERVAL_SECONDS`). It only keeps the stored status honest; correctness does not depend on it. Reads report `EXPIRED` even before the sweeper has run.
- **`availableSeats = max(0, totalSeats - held - confirmed)`.**
- **All times are UTC**, ISO-8601 with milliseconds and `Z`, taken from the backend clock. Expiry uses the application server's clock, not the database's. Both backend containers run on the same host, so there is no clock skew between them; across real machines the database clock (`now()`) would be better.

## Concurrency

- **A class never exceeds `totalSeats` because every seat decision runs in one transaction that first locks the class row with `SELECT ... FOR UPDATE`.** Hold, confirm and cancel for the same class queue up on that lock, so "count, then insert" can never interleave. The lock lives in PostgreSQL, so it also works across several backend processes, which is what the bonus asks for.
- **Defence in depth:** a partial unique index (`ClassId, StudentId` where status is `HELD` or `CONFIRMED`) enforces one active application per student per class even if the application logic were wrong. Confirm and cancel use conditional `UPDATE ... WHERE Status = 'HELD' AND ExpiresAt > now`, so they stay correct against the sweeper.
- **Before inserting, stale `HELD` rows of the same class are marked `EXPIRED`** (under the lock), otherwise an expired-but-not-yet-swept row would block the unique index and stop a student from re-applying.
- **The in-process "two backend instances" xUnit test is not two OS processes.** It starts two application hosts in one test process, each with its own connection pool, against one database. It is a fast regression check, not the proof. The proof is `scripts/two-backend-proof.mjs`, which uses two real backend containers (ports 8080 and 8081) on one PostgreSQL; see the README.
- **Migrations run at start-up under a PostgreSQL advisory lock**, so two backends starting together do not migrate at the same time. Data lives in the named volume `pgdata`.

## Classes

- **Class name is required (up to 200 characters); `totalSeats` is a whole number from 1 to 1,000,000.** Zero or negative seats make no sense for a class; the upper limit only prevents typos like an extra `0`.
- **Duplicate class names are allowed.** The brief does not forbid it, and forbidding it would break re-running the same setup.
- **No edit or delete for classes.** Not in the 8 required endpoints.

## Frontend

- **The student ID is saved in `localStorage` on every keystroke** (so it survives a refresh and closing the browser), and only a valid 17-digit value is used for requests.
- **Seat counts and "My applications" reload every 4 seconds** ("a few seconds" in the brief) and when the tab becomes visible again. Polling pauses while the tab is hidden.
- **The countdown follows the server's clock, not the browser's.** `expiresAt` is a server time, so the browser measures its clock difference from the HTTP `Date` header (whole-second precision, so accurate to about half a second). When the countdown reaches zero the card shows `EXPIRED` immediately and re-checks the server.
- **The Apply button is disabled** when there are no seats, no valid student ID, or the student already has an active application for that class; a hint says why. The server still enforces all three, and the UI shows the server's answer if the screen was out of date.
- **The backend URL is the one the *browser* uses** (`API_URL`, default `http://localhost:8080`), written to `config.js` when the frontend container starts. CORS on the backend allows any origin because there are no cookies or credentials.
- **Plain HTTP only.** No TLS; out of scope for a local exercise.
