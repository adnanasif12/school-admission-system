# AI usage

## Which AI tool

**Claude Code** (Anthropic's coding agent) inside VS Code, running the **Claude Sonnet 5.5** model (`claude-sonnet-5-5`).
It could read and write files in the repository, run shell commands, and use Docker on my machine.
I gave it the assignment PDF and instructions one phase at a time (backend, then frontend, then docs and bonus).

## Where it was used

| Area | What the AI did |
|---|---|
| Reading the brief | The PDF's text layer lost all Bengali text, so the AI rendered the pages as images and read the API contract from them. |
| Backend (`backend/src`) | Wrote the .NET 8 API, EF Core model and migration, the `SELECT ... FOR UPDATE` seat logic, the expiry background service and the Dockerfile. |
| Backend tests | Wrote the xUnit tests (concurrency, expiry, idempotent confirm, ownership, JSON contract) and the throw-away-database test setup. |
| Frontend (`frontend/src`) | Wrote the React screens, polling, countdown, error-message mapping, error boundary, nginx image and the entrypoint that writes `config.js`. |
| Tests and proof | Wrote the Vitest unit tests, the Playwright end-to-end test and the two-backend proof script. |
| Docker and docs | Wrote `docker-compose.yml` with its profiles, README, ASSUMPTIONS.md and this file. |

Nothing in this repository was copied from another project.

## How the output was checked (not just trusted)

- The AI ran everything itself: backend tests, frontend unit tests, a browser test against the real containers, and `curl` against the live API, comparing every status code and JSON field name with the PDF.
- It checked that data survives `docker compose down` then `up`, and that a hold expires with no API call (it read the database row directly).
- It made the tests fail on purpose to prove they can: see mistake 9.

## Mistakes the AI made, and how they were caught

1. **Class list returned HTTP 500.** The query sorted *after* projecting into a record, which EF Core cannot translate to SQL. It compiled fine and the unit-level reasoning looked right; it only failed when the real container was called with `curl`. Fixed by ordering and filtering before the projection.
2. **Wrong .NET API and missing import.** It used `JsonSerializerOptions.Web` (only exists in .NET 9) and forgot the `using` for `GetDbTransaction`. The compiler caught both.
3. **Environment variable silently ignored.** `HOLD_DURATION_SECONDS=15 docker compose up` did not reach the container in Git Bash on Windows, so the "expiry" check still saw a 600-second hold and looked broken. Caught by running `printenv` inside the container; fixed by using `export`, and the README now says so.
4. **Test-isolation bug I introduced while speeding tests up.** Sharing one database per test class made a `/me/holds` test see other tests' holds (3 results instead of 2). Fixed with a unique student id. The change cut the suite from about 2.5 minutes to about 35 seconds.
5. **Bugs in its own browser test script (not in the app).** It matched seat labels with `innerText`, which returns the CSS-uppercased text, so "Available 1" never matched. It reused the same student ids across runs, so a second run found extra applications. It also built an 18-digit id once. Each failure was investigated before blaming the app.
6. **Countdown off by a second.** A 30-second hold showed `0:31`. The server's HTTP `Date` header is truncated to whole seconds, so the browser's clock offset was up to a second behind. Fixed by using the midpoint (about +/-0.5 s). It can still show `0:31` for a moment at the very start; the server remains the authority when the timer reaches zero.
7. **A wrong claim about CRLF.** After I asked for LF line endings, the AI said its file-writing tool produced CRLF files, based on `grep -c $'\r'` returning 11. The count was wrong because the shell dropped the `\r` from the pattern, so it matched every line. Checking real bytes (`tr -cd '\r' | wc -c`) gave 0. It corrected the claim. The CRLF-tolerant entrypoint was still tested for real by converting the script to CRLF, building the image and starting it.
8. **Overstated what the "two backend instances" test shows.** The first version of the test starts two app hosts inside one test process. That is not two OS processes. The AI said so, added the real two-container proof, and ASSUMPTIONS.md states the difference.
9. **Proving the concurrency tests can fail.** The AI removed `FOR UPDATE` on purpose. The 60-request test let 15 holds through on a 5-seat class, and the two-instance test let 23 through on 7 seats. The same was repeated for the two-container proof script (see the README for the outcome). The code was then restored from git.
10. **Tool limits it had to report instead of hide.** Neither of my connected Chrome browsers could open `localhost`, so it did not claim a manual UI check; it used headless Chromium in Docker and said why. Port 3000 on my machine was used by another project, so it made the port configurable (`FRONTEND_PORT`) instead of stopping that container. When a session was interrupted in the middle of a Docker build, it re-checked the real state before continuing.

## Candidate notes

_(To be filled in by me: what I asked for, what I changed or rejected, what I reviewed myself, and what I would do differently.)_

-
