---
title: "stale-allow (AXG002): remove invalid allowances"
linkTitle: stale-allow
description: Reports allowances, notes and capability claims that are malformed, misspelled or no longer needed.
---

# stale-allow (AXG002)

## The problem

An allowance outlives its reason. The raw call it covered gets replaced, or the rule name has a typo and the comment
never covered anything. A reader still sees `// axial-allow-…` and assumes something here is risky. Worse, a stale
allowance would silently cover a new raw call added to the same line later.

```fsharp
let readNow () = Clock.now // axial-allow-raw-effect: (why: reads the real clock)
```

`Clock.now` is not a raw call, so the allowance covers nothing. Delete it.

## What it reports

- An allowance that covers no finding.
- An allowance that is not needed: the file claims the capability or is a `host` file, or the level is `forbid`,
  which no allowance overrides.
- A malformed comment, an unknown rule name, or a missing reason.
- The old numbered spelling, such as `axial-allow-AXG001`. The finding gives the name to use.
- A `raw-effect` allowance without `(gap: …)` or `(why: …)`, or one that tries to cover a whole file.
- An allowance for a rule that takes none: `swallowed-exn`, `cancel-none`, `dropped-work`, `guessed-wait`,
  `task-surface`.
- A note that is malformed, misplaced, stale or on a block finding. These warn, and are errors at a checkpoint.
- A capability claim on a file that makes no raw call for it. This is a warning naming the claim to remove from the
  project file.

## The fix

Remove the comment. When it sits on its own line, the analyzer offers to remove it for you. When the exception is
real, fix the comment: name the rule, give the reason, and for `raw-effect` say the gap or why.
