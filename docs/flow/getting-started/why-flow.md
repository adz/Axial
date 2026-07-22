---
weight: 1
title: Why Flow?
description: Why an application may need more than Task, Async, and Result composition.
---

# Why Flow?

F# already has good tools for pure functions, expected failures, and asynchronous work. Many applications start with
functions returning `Result`, `Async`, or `Task`, and should keep doing so while those types are enough.

The friction appears when one operation needs several concerns at once:

```fsharp
val loadUser:
    cancellationToken: CancellationToken ->
    services: AppServices ->
    userId: UserId ->
        Task<Result<User, LoadUserError>>
```

The return type records the expected error, but cancellation and dependencies are separate arguments. Exceptions can
still escape the Task, and callers must decide who owns retries, child work, and resource cleanup.

As operations compose, that policy is repeated across the call tree. A caller may pass dependencies it never uses,
catch an exception at the wrong boundary, or start background work without a clear owner.

Flow puts the three parts of a workflow in one type:

```fsharp
val loadUser: UserId -> Flow<AppServices, LoadUserError, User>
```

The signature says that the workflow:

- requires `AppServices`;
- may fail with `LoadUserError`;
- may succeed with `User`.

A Flow is a description, not an already-running Task. The runtime starts that description at an explicit boundary and
owns cancellation, child work, scopes, and cleanup for that execution.

Use Flow for application orchestration and operational work. Keep local validation and ordinary pure composition in
`Result` or another focused type until the code actually needs Flow's execution model.

## Go Further

- [Task vs Flow: seven scenarios]({{< relref "/flow/comparisons/task-vs-flow-scenarios/" >}}) compares ownership,
  cancellation, retries, and background work in concrete examples.
- [Flow compared with Effect-TS]({{< relref "/flow/comparisons/effect-ts-comparison/" >}}) explains the shared model
  and the places where F# leads to a different API.
- [Compiler-directed, AOT, and Fable]({{< relref "/flow/packages-and-platforms/" >}}) describes the supported runtime
  targets and package boundaries.
