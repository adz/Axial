---
title: Why Flow?
description: Why application operations need explicit effect management, and how Flow provides one model for it.
---

# Why Flow?

## Why manage effects?

Pure functions are easy to compose because their result comes only from their arguments.

Application operations are not pure. They also read or change the world: call services, start processes, read files,
wait for time, and send requests. This work can fail, be interrupted halfway through, acquire a resource that must
later be released, or start work that must not outlive its caller.

Managing effects means defining those operational rules once and applying them consistently as operations compose.
Cancellation reaches the whole operation, resources are released when it finishes, expected failures follow one path,
and child work has an owner. Without a common model, each layer has to recreate those rules. One missed token,
exception translation, or cleanup path makes the composed operation less reliable.

An explicit model also makes the contract visible. Callers can see which services an operation needs, which failures
it expects, and who owns its lifetime. Tests can supply those services directly instead of discovering hidden behavior.

## One model as effects compose

`Task`, `Result`, cancellation tokens, `IDisposable`, and dependency injection each solve a different concern. As
application operations compose, every layer has to keep those conventions aligned.

`Flow<environment, error, value>` gives effectful application operations one contract. The environment is its
capabilities: values it is allowed to use, usually service dependencies but also configuration or request context.
The error records expected failure, and the value records success. When you start a Flow, its runtime carries
cancellation and owns child work and scoped cleanup. Services still perform the I/O; Flow coordinates it.

Return `Flow` when an application operation first has an operational concern that must compose with other operations:
a capability supplied by the caller, a named expected failure, cancellation, a timeout or retry policy, a resource
lifetime, or owned background work. The next concern uses the same model instead of adding a calling convention or
adapter. Keep pure domain transformations in ordinary functions and `Result` values.

## Compare the signatures

This `Task` signature accepts a dependency and cancellation token, and returns an expected failure:

```fsharp no-check reason="Illustrative signature; AppServices and LoadUserError belong to the reader's application"
val loadUser:
    CancellationToken
    -> AppServices
    -> UserId
    -> Task<Result<User, LoadUserError>>
```

The return type records the expected error. The caller must pass cancellation and dependencies separately. Exceptions
can still escape the `Task`, and the signature does not say who owns retries, child work, or cleanup.

Flow puts the dependency and expected failure in one type. It moves cancellation from a caller argument into the
execution model:

```fsharp no-check reason="Illustrative signature; AppServices and LoadUserError belong to the reader's application"
val loadUser: UserId -> Flow<AppServices, LoadUserError, User>
```

The signature says that the workflow requires `AppServices`, can fail with `LoadUserError`, and can return `User`.
Callers do not pass a token because the runtime that starts the workflow owns cancellation.

## A Flow is a description

A `Flow` value does not start work when you create it. Work starts only at an explicit boundary. That boundary owns
cancellation, child work, scopes, and cleanup for the execution. This separation has two consequences:

- You can build, pass, store, and compose a workflow before you decide where to run it.
- Retries and schedules operate on a workflow description instead of on hand-written loops around a running task.

Use Flow for application orchestration and operational work. Keep local validation and pure composition in `Result`
or another focused type.

## Related guides

- [Task vs Flow: seven scenarios](/how-it-compares/task-vs-flow-scenarios.html) compares ownership, cancellation,
  retries, and background work in concrete examples.
- [Flow compared with Effect-TS](/how-it-compares/effect-ts-comparison.html) explains the shared model and the
  places where F# leads to a different API.
- [Compiler-directed, AOT, and Fable](/notes/packages-and-platforms.html) describes the supported runtime targets and
  package boundaries.
