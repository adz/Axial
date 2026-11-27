---
title: Allowances and notes
linkTitle: Allowances and notes
description: Permit one deliberate raw call with an allowance, or acknowledge a guide finding with a note.
---

# Allowances and notes

Sometimes the finding is right and you still need the call. Axial may have no service for it yet, or the code is
a test of the real thing. Guardrails lets you say so on the line, with a reason a reviewer can read.

## Allow one call

An allowance is a comment on the line, or the line before. It names the rule and ends with either the missing
feature, `(gap: name)`, or why the call is deliberate, `(why: reason)`:

```fsharp
let pipe = new System.IO.Pipes.NamedPipeServerStream("app-control") // axial-allow-raw-effect: Axial.Process has no named pipes yet (gap: process-named-pipes)
```

```fsharp
// axial-allow-raw-effect: (why: this test checks that the analyzer reports a real clock read)
let now = DateTime.UtcNow
```

An allowance covers one call. It cannot cover a file, and it cannot override `forbid`.

Use one only when nothing else works. First try the fix the finding names. If the file implements the service,
[claim the capability](capabilities.html#claim-a-capability) instead. `axial check` counts allowances per gap and
per why, and policy review lists every new one first, so a gap that keeps coming up is visible.

Which rules take an allowance:

| Rule | Allowance |
| --- | --- |
| `raw-effect` | `// axial-allow-raw-effect: <reason> (gap: name)` or `(why: reason)` |
| `raw-concurrency` | `// axial-allow-raw-concurrency: (why: reason)` or `(gap: name)` |
| `raise-in-flow`, `shared-fixture`, `dropped-cancel`, `reflection-format`, `fiber-resource`, `escaped-scope` | `// axial-allow-<rule>: <reason>` |
| `swallowed-exn`, `guessed-wait`, `task-surface` | None; use a note |
| `cancel-none`, `dropped-work` | None |

## Acknowledge a guide finding with a note

Three rules are guides rather than blocks. They point at code that is often wrong but sometimes right:
`swallowed-exn`, an awaited delay under `guessed-wait`, and `task-surface`. For these you leave a note:

```fsharp
let acknowledged () =
    try
        failwith "failed"
    with _ ->
        // axial-note swallowed-exn: best-effort cleanup; the failure is reported elsewhere
        ()
```

The finding stays in the build output as Info, with your reason. A note never hides a finding.

A note goes on the finding's line or the line before it. For `swallowed-exn`, that is the first line inside the
handler, not above `with`.

## Checkpoints

A checkpoint build is a stricter build for CI and releases. Set `-p:AxialGuardrailsCheckpoint=true`, or run
`axial check --full`, which sets it. At a checkpoint:

- A `swallowed-exn` or awaited-delay `guessed-wait` finding without a note is an error.
- A malformed, misplaced or stale note is an error.
- `task-surface` stays Info.

An ordinary build, Release included, is not a checkpoint.

## Invalid comments

[`stale-allow`](diagnostics/stale-allow.html) reports an allowance or note that is malformed, names an unknown rule,
has no reason, or no longer covers a finding. Every allowance and note is recorded in the policy lock, so each new one
shows up in [policy review](policy-review.html).
