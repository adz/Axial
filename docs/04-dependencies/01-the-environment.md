---
title: The Environment
description: What the 'env parameter actually is, and the handful of functions that read it.
---

# The Environment

There is no container and no registration step. `'env` is an ordinary type parameter, and the value you supply is an
ordinary value.

```fsharp
open Axial

let doubled : Flow<int, Never, int> =
    Flow.envWith (fun environment -> environment * 2)

let result = doubled |> Flow.run 21    // Success 42
```

The environment here is an `int`. Nothing about `Flow` requires a record, an interface, or a service — it hands your
function whatever value you passed to `Flow.run`, and that is the entire mechanism.

## What the functions do

`Flow.envWith` **runs a function against the environment** and continues with the result:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
Flow.envWith (fun environment -> environment.Users)   // 'env -> 'a, giving Flow<'env, _, 'a>
```

`_.Users` is F# shorthand for `fun environment -> environment.Users`, so `Flow.envWith _.Users` is the same thing
written shorter.

The rest of the environment surface is equally small:

| Function | What it does |
| --- | --- |
| `Flow.envWith projection` | Runs `projection` against the environment, continues with its result |
| `Flow.envWith id` | Continues with the environment value itself |
| `Flow.localEnv change` | Runs a flow against a *different* environment computed by `change` |

`Flow.localEnv` is how a workflow needing a small environment runs inside one that has more:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let usersOnly : Flow<IUserStore, AppError, User> = ...

let inTheApp : Flow<AppEnv, AppError, User> =
    usersOnly |> Flow.localEnv (fun app -> app.Users)
```

## What you will actually use

An `int` proves the point but is not the shape you want. In practice the environment is **a record you define**,
holding one field per dependency:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type AppEnv =
    { Users: IUserStore
      Audit: IAuditLog }

let loadUser id : EnvFlow<AppEnv, User> =
    flow {
        let! users = Flow.envWith _.Users
        return! users.Load id
    }
```

You construct that record in exactly two places:

- **At boot**, with the live implementations.
- **In tests**, with fakes — the same record type, different values.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let live = { Users = SqlUserStore(connection); Audit = FileAuditLog(path) }
let underTest = { Users = InMemoryUserStore(); Audit = NullAuditLog() }

loadUser 42 |> Flow.run live
loadUser 42 |> Flow.run underTest
```

Larger systems often define one record per architectural boundary rather than a single application-wide one, and use
`Flow.localEnv` to move between them. A billing subsystem that cannot see the mailer is a record without a mailer
field, and that is enforced by the compiler rather than by convention.

## Where the rest of the section goes

That is the whole model. Everything after this page exists for cases the plain record does not cover:

- [Choosing an approach](choosing-an-approach.html) — when arguments beat a record, and when a record stops being
  enough.
- [Service contracts](service-contracts.html) — how a *package* asks for a dependency without knowing your record
  type.
- [Providing the environment](providing-the-environment.html) — building the value at a host boundary.
