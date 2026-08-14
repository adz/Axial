---
title: The Flow CE
description: Sequence dependent workflow steps with flow { }.
---

# The Flow CE

Use `flow {}` when later work depends on earlier success.

Suppose the block calls these functions:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let loadUser (id: UserId) : Flow<AppEnv, AppError, User> = ...
let auditUser (user: User) : Flow<AppEnv, AppError, unit> = ...
let greetUser (user: User) : Flow<AppEnv, AppError, string> = ...
```

`let!` binds a successful value to the name on its left. `do!` binds a step whose success value is `unit`.
`return!` uses another complete Flow as the result of the block.

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
flow {
    let! user = loadUser userId
    do! auditUser user
    return! greetUser user
}
```

Here is the same block with the important left- and right-hand types shown:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
flow {
    let! (user: User) =
        (loadUser userId: Flow<AppEnv, AppError, User>)

    do! (auditUser user: Flow<AppEnv, AppError, unit>)
    return! (greetUser user: Flow<AppEnv, AppError, string>)
}
// Flow<AppEnv, AppError, string>
```

`flow {}` also binds `Result`, `Option`, `ValueOption`, `Async`, and `ColdTask`. An outer `Result.Error` enters the
Flow error channel. Raw `Task` and `ValueTask` values do not bind directly; use `ColdTask` for work that should start
with the Flow or an explicit `Flow.awaitStarted*` function for work already running. The output remains one cold Flow
description until an execution boundary runs it.

Normal F# `if`, `match`, `for`, and `while` expressions work inside the computation expression.

## Go Further

- [Flow builder reference](/api.html) lists the values accepted by each
  computation-expression operation.
- [Bind](/error-handling/bind.html) covers bind-site error assignment and mapping when the source
  error does not already match the workflow.
- [Task and Async interop](/the-flow-type/task-async-interop.html) gives the detailed carrier and
  cancellation rules.
