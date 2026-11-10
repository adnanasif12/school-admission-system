# AI usage

**Tool:** Claude Code (Anthropic) in VS Code, model **Claude Sonnet 5.5** (`claude-sonnet-5-5`). It read and wrote files and ran shell and Docker commands. I gave it the assignment PDF and instructions one phase at a time. Nothing was copied from another project.

## Where it was used

| Area | What the AI did |
|---|---|
| Backend | API, EF Core model and migration, row-lock seat logic, expiry service, Dockerfile |
| Backend tests | xUnit tests, including the concurrency tests |
| Frontend | React screens, polling, countdown, plain-language errors, nginx image |
| Tests, proof, docs | Vitest, Playwright end-to-end test, two-backend proof, compose file, README |

## Mistakes caught

1. **Proving the tests can fail.** I had the AI remove `FOR UPDATE` on purpose. The unit test let 15 holds through on a 5-seat class, and the two-container proof accepted 20 on a 1-seat class. The line was restored afterwards.
2. **HTTP 500 on the class list.** An EF query sorted after projecting, which cannot be translated to SQL. It compiled; only a real `curl` against the container exposed it. Fixed by ordering first.
3. **Countdown off by a second.** A 30-second hold showed `0:31` because the server's `Date` header is truncated to whole seconds. Now corrected to about half a second.
4. **A wrong CRLF claim.** The AI said its tool wrote CRLF files, based on a `grep` that matched every line. Counting real bytes gave 0. It corrected itself and tested the entrypoint with genuine CRLF.
5. **In-process vs real two-backend test.** The first "two instances" test ran two hosts inside one process. The AI said that is not two OS processes and built a proof with two containers.

## Candidate notes

_(To be filled in by me: what I asked for, what I changed or rejected, what I reviewed myself, and what I would do differently.)_

-
