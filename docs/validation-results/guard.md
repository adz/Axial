---
weight: 25
title: The Guard
type: docs
description: Bridging pure checks and effectful sources into Flow.
---

# The Guard

The [**Guard**]({{< relref "/reference/flow/t-flow.md" >}}) is a bridge. It allows pure predicate checks and simple error-bearing sources (like `Option` or `Result`) to **fail a computation** with a specific domain error.

While builders like [`flow {}`]({{< relref "/reference/flow/builders-flow.md" >}}) and `result {}` can bind many types directly, Guard is the primary tool for assigning a domain-specific error to a source or "unwrapping" an effectful source before validation.

## Why do I need Guard?

Consider a simple user lookup. When it is a pure function, it works seamlessly with [`Check`]({{< relref "/reference/check/" >}}):

```fsharp
open FsFlow
open FsFlow.Check

type User = { Name: string }
type AppError = UserNotFound | InvalidPassword

// A pure "database" lookup
let tryGetUser name : User option =
    let users = [ { Name = "ada" }; { Name = "bob" } ]
    users |> List.tryFind (fun u -> u.Name = name)

let loginPure name =
    [`result {}`]({{< relref "/reference/result/builders-result.md" >}}) {
        // This works: okIfSome expects an Option, which tryGetUser returns
        let! user = tryGetUser name |> okIfSome |> orError UserNotFound
        return user
    }
```

However, in a real application, lookup usually requires a database connection from the environment or performs async work. Now it becomes a [`Flow`]({{< relref "/reference/flow/t-flow.md" >}}):

```fsharp
// In reality, this needs the environment and is effectful
let tryGetUserFlow name : Flow<DbEnv, AppError, User option> =
    flow {
        let! db = Flow.read _.Db
        return! db.Users.TryFind name
    }
```

If you try to use the same `Check` pattern inside a `flow {}` block, it will fail to compile:

```fsharp
let login name =
    flow {
        // COMPILE ERROR: tryGetUserFlow returns a Flow, but okIfSome expects an Option.
        // The builder hasn't "unwrapped" the flow yet!
        let! user = tryGetUserFlow name |> Check.okIfSome |> Check.orError UserNotFound
        
        return user
    }
```

`Check` functions expect pure types (like `Option` or `string`). They don't know how to look "inside" a Flow. 

[**Guard**]({{< relref "/reference/flow/t-flow.md" >}}) is the bridge. It allows you to "mark" an effectful source so that the flow builder binds it first before applying the check:

```fsharp
let login name password =
    flow {
        // 1. Guard binds the flow, sees the Option, and applies the error logic
        let! user = tryGetUserFlow name |> Guard.Of UserNotFound
        
        // 2. You can also guard pure checks that haven't been lifted yet
        do! notBlank password |> Guard.Of InvalidPassword
        
        return user
    }
```

## Common Guard Patterns

### Guarding Options
Convert a `Some` value into success and `None` into a specific error:
```fsharp
let! user = maybeUser |> Guard.Of UserNotFound
```

### Guarding Checks
Apply a pure predicate and fail the flow if it returns `Error ()`:
```fsharp
do! notBlank password |> Guard.Of InvalidPassword
```

### Remapping Errors
If a source already has an error, but it doesn't match your flow's error type, use `Guard.MapError`:
```fsharp
let! user = fetchUser id |> Guard.MapError (fun ex -> DatabaseError ex.Message)
```

## Summary

Use **Guard** whenever you need to:
- Assign a domain error to a pure `Check` or `Option` inside a computation.
- "Unwrap" an effectful source that returns a simple type before validating it.
- Keep your business logic readable by avoiding manual matching or re-lifting inside your flows.
