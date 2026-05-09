---
weight: 30
title: Capabilities (CAPS)
description: Deep dive into the interface-based CAPS pattern for decoupled dependencies.
---

# Capabilities (CAPS)

The CAPS pattern lets you decouple your workflows from specific record types. Instead of saying "I need the `AppEnv` record," a workflow says "I need any environment that provides an `IClock`."

This is achieved using the `Needs<'dep>` interface and the `Env<'dep>` request token.

## Requesting a Dependency with `Env<'dep>`

In a CAPS workflow, you use the `Env<'dep>` token to request a dependency by its type. FsFlow handles finding that dependency in the environment for you.

```fsharp
let getTime =
    taskFlow {
        // Request the IClock dependency directly
        let! clock = Env<IClock> 
        return clock.UtcNow()
    }
```

### Projecting with `Env<'dep>`

Just like `TaskFlow.read` in the Record Pattern, you can project a value or call a method directly from the dependency using a lambda or the `_.Field` shorthand.

```fsharp
let readTime =
    taskFlow {
        // Project UtcNow from the IClock dependency
        let! now = Env<IClock> (fun clock -> clock.UtcNow())
        
        // Or use the shorthand
        let! now2 = Env<IClock> _.UtcNow
        
        return now
    }
```

## Satisfying the Contract with `Needs<'dep>`

To run a CAPS workflow, your environment must implement the `Needs<'dep>` interface. This acts as the "glue" between your concrete implementation and the workflow's requirements.

```fsharp
type MyRuntime =
    { ClockService: IClock }

    interface Needs<IClock> with
        member x.Dep = x.ClockService
```

## Named Cap Sets

For complex boundaries, you can group multiple dependencies into a single named interface. This is common for "Use Case" boundaries or library APIs.

```fsharp
type LoginCaps =
    inherit Needs<IUserStore>
    inherit Needs<IClock>
    abstract UserStore : IUserStore
    abstract Clock : IClock

let login email : TaskFlow<#LoginCaps, AppError, Session> =
    taskFlow {
        let! store = Env<IUserStore>
        let! clock = Env<IClock>
        
        let! user = store.FindByEmail email
        let! now = clock.UtcNow()
        
        return { Email = user; IssuedAt = now }
    }
```

### Using Flexible Types (`#`)

By using the `#LoginCaps` constraint (the "flexible" or "hash" type), this workflow can accept any environment that implements `LoginCaps`, or even a larger runtime that just happens to satisfy the requirements.

## Running on a Larger Runtime

A runtime can be much larger than the specific cap set the flow asks for.

```fsharp
type AppRuntime =
    { UserStoreService : IUserStore
      ClockService : IClock
      LoggerService : ILogger
      Database : IDatabase }

    interface LoginCaps with
        member x.UserStore = x.UserStoreService
        member x.Clock = x.ClockService

    interface Needs<IUserStore> with
        member x.Dep = x.UserStoreService

    interface Needs<IClock> with
        member x.Dep = x.ClockService

// Even though AppRuntime is huge, it can run the login flow
login "ada@example.com"
|> TaskFlow.run appRuntime CancellationToken.None
```

## Testing Stays Small

For tests, you only need to implement the specific caps the flow requires, rather than mocking an entire application record.

```fsharp
type TestRuntime =
    { Clock : IClock }
    interface Needs<IClock> with member x.Dep = x.Clock

getTime |> TaskFlow.run { Clock = MockClock() } CancellationToken.None
```

---

## Next Steps

If you want to use simpler record-based environments without interfaces, see the [Record Pattern (Environment Slicing)](./env-slicing/) guide.
