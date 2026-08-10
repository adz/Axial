---
title: Why Flow?
description: When an application needs more than Task, Async, and Result composition, and when it does not.
---

# Why Flow?

## Do you need Flow?

You do not need Flow for pure functions, local validation, or a single `Task` call. Keep using `Result`, `Async`, and
`Task` while they are enough. F# is good at those, and Flow adds a type parameter and a runtime that a small function
does not repay.

Reach for Flow when one call tree carries several of these at once:

- dependencies that a test has to replace;
- expected failures that callers must handle by name;
- cancellation that has to reach every inner call;
- retries, timeouts, or scheduled repetition;
- resources whose release must survive a failure;
- background work that needs an owner.

Any one of those is manageable by hand. The cost is that each is a separate mechanism, and each caller in the tree
has to repeat the policy correctly.

## The signature is the argument

Here is one operation with three of those concerns, written against `Task`:

```fsharp no-check reason="Illustrative signature; AppServices and LoadUserError belong to the reader's application"
val loadUser:
    cancellationToken: CancellationToken ->
    services: AppServices ->
    userId: UserId ->
        Task<Result<User, LoadUserError>>
```

The return type records the expected error. Cancellation and dependencies are separate arguments the caller must
remember to thread through, exceptions can still escape the `Task`, and nothing states who owns retries, child work,
or cleanup.

Flow puts the same three parts in one type:

```fsharp no-check reason="Illustrative signature; AppServices and LoadUserError belong to the reader's application"
val loadUser: UserId -> Flow<AppServices, LoadUserError, User>
```

The signature says that the workflow requires `AppServices`, can fail with `LoadUserError`, and can succeed with
`User`. Callers do not pass a token, because the runtime that starts the workflow owns cancellation.

## A Flow is a description

A `Flow` value is not an already-running `Task`. Nothing happens until you start it at an explicit boundary, and that
boundary owns cancellation, child fibers, scopes, and cleanup for the execution. Two consequences follow:

- Building a workflow twice and running it twice is safe, so retries and schedules are ordinary combinators rather
  than hand-written loops.
- A workflow value can be passed around, stored, and composed before anyone decides to run it.

Use Flow for application orchestration and operational work. Keep local validation and ordinary pure composition in
`Result` or another focused type until the code actually needs Flow's execution model.

## Go further

- [Task vs Flow: seven scenarios](/how-it-compares/task-vs-flow-scenarios.html) compares ownership, cancellation,
  retries, and background work in concrete examples.
- [Flow compared with Effect-TS](/how-it-compares/effect-ts-comparison.html) explains the shared model and the
  places where F# leads to a different API.
- [Compiler-directed, AOT, and Fable](/notes/packages-and-platforms.html) describes the supported runtime targets and
  package boundaries.
