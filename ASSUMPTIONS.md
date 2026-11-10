# Assumptions

What the brief left open, and what I chose (short reason in brackets).

## Identity and access
- Student ID is exactly 17 digits in `X-Student-Id`; missing or malformed gives 400 (no auth scheme).
- No login or admin authentication (brief: no login).
- Someone else's hold: 403 on confirm or cancel, 404 if unknown (different problems).
- `GET /holds/{id}` needs no header, 403 if a non-owner's header is sent (brief's example has none).

## Applications
- Re-applying after cancel or expiry is allowed (only `HELD` and `CONFIRMED` are active).
- Different classes can be held at once (limit is per class).
- A `CONFIRMED` application cannot be cancelled: 409 (fee paid, no refunds in brief); cancelling twice gives 204.
- Confirm needs your own, unexpired `HELD` hold; a repeat gives 200 and counts once; expired or cancelled gives 409.
- "Pay fee" is simulated (per brief).
- `HOLD_DURATION_SECONDS` defaults to 600; invalid values fall back to 600.

## Seats and expiry
- Lazy expiry: counts ignore `HELD` rows past `expiresAt` (seat returns instantly, no API call).
- A background sweeper (every 5 s) also stores `EXPIRED` (tidy data; counts don't depend on it).
- Times are UTC, ISO-8601, from the backend clock (one clock decides).

## Concurrency
- Seat decisions run in a transaction with `SELECT ... FOR UPDATE` on the class row (requests queue, works across processes).
- A partial unique index allows one active hold per student per class (backstop).
- The xUnit "two instances" test is one process with two hosts, not two OS processes; the real proof is `scripts/two-backend-proof.mjs` with two containers.

## Classes
- Name required (max 200 characters), seats 1 to 1,000,000 (catches typos).
- Duplicate names allowed; no edit or delete (not among the 8 endpoints).

## Frontend
- Lists reload every 4 s (brief: "a few seconds"), paused in hidden tabs.
- Countdown follows the server clock via the `Date` header; at zero it shows `EXPIRED` and re-checks.
- Apply is disabled with no seats, no valid ID or an active application (server enforces it anyway).
- CORS allows any origin (no cookies or credentials).
