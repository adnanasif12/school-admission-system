# AI usage

**Tool:** Claude Code (Anthropic) in VS Code, model **Claude Sonnet 5.5** (`claude-sonnet-5-5`). I gave the AI the assignment PDF and instructions one phase at a time.

## Where it was used

| Area | What the AI did |
|---|---|
| Backend | .NET 8 API, EF Core model and migration, row-lock seat logic, expiry service |
| Tests | xUnit (including concurrency), Vitest, Playwright end-to-end, two-backend proof script |
| Frontend | React screens, polling, countdown, plain-language error messages |
| Docker and docs | Dockerfiles, compose file, README, ASSUMPTIONS.md, this file |

## How the output was checked

The AI ran the backend and frontend tests, called the live API with `curl` against the PDF's contract, drove the UI in a headless browser, and ran the app from a fresh clone. I also tested the API and the UI myself.

## Mistakes caught

1. **`FOR UPDATE` removed on purpose.** To prove the concurrency tests can fail, the AI deleted the row lock. 15 holds got through on a 5-seat class, and 20 on a 1-seat class in the two-container proof. It then restored the line.
2. **HTTP 500 from an EF Core query.** The class list sorted after projecting, which EF cannot translate to SQL. It compiled fine and only failed against the real container.
3. **Countdown off by a second.** A 30-second hold showed `0:31` because the server's `Date` header is truncated to whole seconds. The AI changed it to use the midpoint (about half a second).
4. **Wrong CRLF claim.** The AI said its tool wrote CRLF files, based on a `grep` that matched every line. Counting real bytes showed 0, and the AI corrected the claim.
5. **In-process vs real two-backend test.** The first "two instances" test ran two hosts in one process. The AI flagged that this is not two OS processes and added a proof with two containers.

## Candidate notes

- I planned the work in three phases (backend, frontend, docs and bonus) and checked each phase before moving on.
- I tested the API myself with PowerShell (create class, hold, 409 when full, confirm, cancel) and checked that data survives `docker compose down` and `up`.
- I tested the UI by hand in the browser: apply, countdown, pay, cancel and expiry.
- I asked for the tests to run with Docker only, because the reviewers will only have Docker.
- Next time I would write the core seat logic and the concurrency test myself first, then use AI for the UI and docs.
