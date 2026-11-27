---
title: Guardrails
linkTitle: Guardrails
description: Axial.Guardrails finds ambient effects, exceptions in workflows and unowned concurrency during the build.
---

# Guardrails

A flow that reads `DateTime.UtcNow` compiles and works. Its test does not: the test installs a test clock with
`Flow.runTest`, and the flow never asks for it. Nothing in the function's signature says it reads the clock, so the
reviewer cannot see it either.

This happened in Axial itself. `Schedule.jittered` created its own `System.Random`, so a test could not fix its
jitter, and `FiberDump` read `DateTimeOffset.UtcNow`, so its timestamps ignored the test clock. Both compiled.

`Axial.Guardrails` catches this during the build:

```fsharp
open System

/// Reported.
let isExpiredByWallClock (deadline: DateTimeOffset) =
    DateTimeOffset.UtcNow > deadline   // error raw-effect: ... use Axial.IClock (Clock.now)
```

```fsharp
/// Fixed.
let isExpired (deadline: DateTimeOffset) =
    flow {
        let! now = Clock.now
        return now > deadline   // the test clock decides
    }
```

Guardrails is optional. Core `Axial` needs no analyzer and no policy file, so you can prototype without it. Add it
when the code settles, or when a coding agent writes it: an agent takes the shortcut a reviewer misses, and the build
then catches it every time.

Other rules catch an exception raised inside `flow { }`, a `Task` that nothing owns, a cancellation token that is
thrown away, and a test that sleeps and hopes. Each finding says what to use instead.

The `axial` command, from the `Axial.Tool` package, manages the policy file and its review.

## Start here

1. [Get started](getting-started.html): install the package, create the policy, see a first finding.
2. [The policy file](policy-file.html): roles, rule levels and which files may make raw calls.
3. [Allowances and notes](allowances-and-notes.html): mark a deliberate raw call or exception.
4. [Fast and Full](fast-and-full.html): the quick default build check and the complete one.
5. [Policy review](policy-review.html): how loosened policy is reviewed and approved.
6. [Adopt with a baseline](baseline.html): accept the findings an existing codebase already has.
7. [Guardrails per package](per-package.html): turn rule sets on and off.

## Reference

- [Diagnostics](diagnostics/index.html): one page per rule, with what it reports and how to fix it.
- [Capabilities](capabilities.html): define your own service that owns a raw API.
- [Package rules](package-rules.html): ship simple rules with a package.
- [Write an effect analyzer](writing-effect-analyzers.html) and [package it](analyzer-packaging.html).
- [Build reports](build-reports.html): the report each build writes, timings and caching.
