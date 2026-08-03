# Review threads

Multi-agent design reviews live in one folder per reviewer, one file per round:

```
dev-docs/<reviewer>/<YYYY-MM-DD>-round-<N>.md
```

Rules:

- **Never append to a file someone else has already read.** A round is immutable once written; the reply is a
  new file in the replier's own folder. This keeps each read bounded instead of re-reading a file that grows
  every exchange.
- Open each round with a one-line pointer to the file it answers, and (once answered) to the file that
  answered it.
- Round numbers are per reviewer and monotonic; the date is the day it was written.

## Active threads

None. The `constraint-unification` thread closed on 2026-08-02 when the joint review was signed; its outcomes
live in `dev-docs/decisions/README.md` and `AGENTS.md`, and the round files were deleted with the spec they
reviewed. Reviewer folders are created per thread and removed with it.
