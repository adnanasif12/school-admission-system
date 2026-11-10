# Assumptions

What the brief left open, and what I chose (reason in brackets).

## Identity
- Student ID is exactly 17 digits in `X-Student-Id`; missing or malformed gives 400 (there is no auth scheme to fail).
- No login and no admin authentication, so anyone can create a class (the brief says no login).
- Confirming or cancelling someone else's application gives 403; an unknown id gives 404 (two different problems).
- `GET /holds/{id}` needs no header (the brief's example shows none); if sent and not the owner, 403.

## Applications
- A student can apply again after a cancel or an expiry (only `HELD` and `CONFIRMED` count as active).
- A student may hold seats in different classes at once (the limit is per class).
- Cancelling a `CONFIRMED` application gives 409 (fee paid, refunds are not in the brief); cancelling twice gives 204.
- Confirm works only on your own, unexpired `HELD` application; a repeat returns 200 and counts once; expired or cancelled gives 409.
- "Pay fee" is simulated, as the brief says.
- `HOLD_DURATION_SECONDS` defaults to 600; an invalid value falls back to 600.

## Expiry
- Lazy: seat counts ignore `HELD` rows past `expiresAt`, so the seat returns at that exact moment.
- A background sweeper (every 5 s) also stores `EXPIRED`; correctness does not depend on it.
- Times are UTC (ISO-8601); the backend clock decides expiry.

## Concurrency
- Every seat decision runs in one transaction holding `SELECT ... FOR UPDATE` on the class row; a partial unique index backs up one active hold per student per class.
- The xUnit "two instances" test runs two hosts inside one process, not two OS processes. The real proof is `scripts/two-backend-proof.mjs` with two backend containers on one database (see README).

## Classes and UI
- Name required (up to 200 characters); seats 1 to 1,000,000; duplicate names allowed; no edit or delete (not among the 8 endpoints).
- Lists reload every 4 s ("a few seconds"), paused while the tab is hidden.
- The countdown follows the server clock via the `Date` header (about 0.5 s accuracy); at zero it shows `EXPIRED` and re-checks the server.
- Apply is disabled with no seats, no valid ID, or an active application; the server enforces all three anyway.
- CORS allows any origin (no cookies or credentials).
