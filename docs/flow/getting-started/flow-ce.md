---
weight: 6
title: The Flow CE
description: Sequence dependent workflow steps with flow { }.
---

# The Flow CE

Use `flow {}` when later work depends on earlier success.

Suppose the block calls these functions:

```fsharp
let loadUser (id: UserId) : Flow<AppEnv, AppError, User> = ...
let auditUser (user: User) : Flow<AppEnv, AppError, unit> = ...
let greetUser (user: User) : Flow<AppEnv, AppError, string> = ...
```

`let!` binds a successful value to the name on its left. `do!` binds a step whose success value is `unit`.
`return!` uses another complete Flow as the result of the block.

```fsharp
flow {
    let! user = loadUser userId
    do! auditUser user
    return! greetUser user
}
```

Here is the same block with the important left- and right-hand types shown:

```fsharp
flow {
    let! (user: User) =
        (loadUser userId: Flow<AppEnv, AppError, User>)

    do! (auditUser user: Flow<AppEnv, AppError, unit>)
    return! (greetUser user: Flow<AppEnv, AppError, string>)
}
// Flow<AppEnv, AppError, string>
```

`flow {}` can also bind compatible `Result`, `Task`, `ValueTask`, and `Async` values. The output remains one Flow
description; execution still waits for an explicit boundary.

Normal F# `if`, `match`, `for`, and `while` expressions work inside the computation expression.

## Go Further

- [Flow builder reference]({{< relref "/flow/reference/flow/builders-flow/" >}}) lists the values accepted by each
  computation-expression operation.
- [Bind]({{< relref "/flow/core-concepts/bind/" >}}) covers bind-site error assignment and mapping when the source
  error does not already match the workflow.
- [Task and Async interop]({{< relref "/flow/core-concepts/task-async-interop/" >}}) gives the detailed carrier and
  cancellation rules.
