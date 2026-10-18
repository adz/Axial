---
title: How It Compares
description: Compare Flow with Task, FsToolkit.ErrorHandling, and Effect-TS, including where each is the smaller choice.
---

# How It Compares

Flow combines typed failure with explicit dependencies and runtime policies. For functions that do not need those
policies, `Task`, `Result`, or a focused library may be enough.

## F# choices

- [Task vs Flow, Seven Scenarios](./task-vs-flow-scenarios.html) implements the same seven programs both ways. It
  compares typed failure, dependency passing, cancellation, resource lifetime, parallel composition, retry, and
  testing, with failure-path tests for each claimed guarantee.
- [FsToolkit.ErrorHandling Comparison](./fstoolkit-errorhandling-comparison.html) compares Flow with `Result`,
  `Async<Result<_, _>>`, and `Task<Result<_, _>>` computation expressions. It explains when a local railway-oriented
  pipeline is sufficient and how to bring a `Task<Result<_, _>>` function into Flow through `ColdTask`.

## The wider effect model

- [Effect-TS Comparison](./effect-ts-comparison.html) explains the shared typed-effect model and why F# applications
  can keep using Axial when they target Node or the browser through Fable. Effect-TS remains a natural integration
  boundary when TypeScript owns the surrounding application.
