---
title: Errors
description: "Every way to produce a value, a typed failure, a defect, or an interruption, run in concurrent branches and then recovered, combined, and converted."
---

# Errors

This scenario produces every kind of [outcome](/error-handling/index.html) from concurrent branches and checks that
each survives being forked, recovered, combined, and converted.

Run it with `dotnet run --project examples/Axial.TortureTest -- errors 100`.

## What it does

- A hundred and twenty branches each produce one outcome in one of fifteen ways: `Flow.ok`, `fail`, `fromResult`,
  `fromOption`, `ofExit`, `die`, a thrown exception, a timeout fallback, several `Policy` checks, and more. Each branch
  is forked and awaited.
- A recovery chain runs over every kind: `Flow.catch`, `tapError`, `mapError`, `tracedError`, `orElseWith`,
  `mapBoth`, and `orElse`.
- The causes are combined with `Cause.both` and `Cause.thenCause`, rendered with `Cause.prettyPrint`, and converted with
  the `Exit` functions.
- `Bind.error` and `Bind.mapError` adapt `Result` errors at bind sites, and the applicative and operator forms run over
  forked fibers.

{{< snippet id="torture-errors" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every forked outcome was reported as itself | Forking and joining keep a value, a typed failure, a defect, and an interruption apart. |
| Recovery turned every outcome into the value it should | Each recovery combinator handles what it documents, including failures wrapped by `Flow.tracedError`, and passes the rest on. |
| Combined causes kept every failure | Combining causes never drops a failure. |
| A traced cause untraces to the cause it wraps | `Cause.untraced` removes trace text without changing the outcome. |
| Exit conversions kept values and failures | `Exit.toResult` and `Exit.fromResult` round-trip, and a cause a `Result` cannot hold is re-thrown. |
| Combinators over forks agree with arithmetic | `map2`, `map3`, `zip`, `apply`, `sequence`, `traverse`, and the operators compute the same results over fibers. |
| Bind adapted each Result error at its bind site | The first `Error` stops the flow with the adapted error. |
| orElseFlow fails with the error its flow produced | The fallback's own failure is returned. |
