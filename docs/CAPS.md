---
title: Using FsFlow CAPS
description: This page shows how to model explicit capability boundaries in FsFlow with `Needs<'dep>` and `Env<'dep>`.
---

# Using FsFlow CAPS

This page shows how to keep a flow honest about the dependencies it needs, without passing a full application runtime everywhere.

FsFlow CAPS are named capability sets built on `Needs<'dep>`. They let a workflow say "I need a store and a clock" instead of "I take whatever the whole app happens to provide."

The code snippets below are illustrative. `IUserStore`, `IClock`, `ILogger`, and the cap-set interfaces are application-specific types that show the intended shape of the API.

## Start with a small, meaningful boundary

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open FsFlow

type IUserStore =
    abstract FindByEmail : string -> Task<string option>

type IClock =
    abstract UtcNow : unit -> DateTimeOffset

type ILogger =
    abstract Info : string -> unit

type UserStoreCaps =
    inherit Needs<IUserStore>
    abstract UserStore : IUserStore

type ClockCaps =
    inherit Needs<IClock>
    abstract Clock : IClock

type LoggerCaps =
    inherit Needs<ILogger>
    abstract Logger : ILogger

type LoginCaps =
    inherit UserStoreCaps
    inherit ClockCaps
    inherit LoggerCaps

type Session =
    { Email : string option
      IssuedAt : DateTimeOffset }

let login : TaskFlow<#LoginCaps, unit, Session> =
    taskFlow {
        let! user = Env<IUserStore> (fun store -> store.FindByEmail "ada@example.com")
        let! now = Env<IClock> _.UtcNow
        do! Env<ILogger> (fun log -> log.Info $"Login at {now}")

        return { Email = user; IssuedAt = now }
    }
```

This is the shape to aim for:

- the type name describes the use case
- the flow only asks for the dependencies it actually needs
- callers can provide a larger runtime, not just an exact match

## Read dependencies with `Env<'dep>`

Use `Env<'dep>` when you need the dependency itself.

```fsharp
taskFlow {
    let! clock = Env<IClock>
    let now = clock.UtcNow()
    return now
}
```

Use `Env<'dep, 'value>` when you want to project a value from the dependency.

```fsharp
taskFlow {
    let! now = Env<IClock> _.UtcNow
    return now
}
```

If the projection returns a `Result`, `Async`, `Task`, `ValueTask`, `ColdTask`, `option`, or `voption`, FsFlow binds it with the same normal workflow rules you already use elsewhere.

## Prefer named cap sets for public boundaries

Named cap sets are useful when the combination means something.

Good:

```fsharp
type ChooseTodoCaps =
    inherit TodoStoreCaps
    inherit RandomCaps

let chooseTodo : TaskFlow<#ChooseTodoCaps, unit, string option> = ...
```

Too broad for a public API:

```fsharp
let chooseTodo : TaskFlow<AppRuntime, unit, string option> = ...
```

Use the smaller, meaningful type. It makes the boundary obvious and keeps tests lighter.

## Run on a larger runtime

A runtime can be larger than the cap set the flow asks for.

```fsharp
type AppRuntime =
    { UserStoreService : IUserStore
      ClockService : IClock
      LoggerService : ILogger
      Audit : string -> unit }

    interface LoginCaps with
        member x.UserStore = x.UserStoreService
        member x.Clock = x.ClockService
        member x.Logger = x.LoggerService

    interface Needs<IUserStore> with
        member x.Dep = x.UserStoreService

    interface Needs<IClock> with
        member x.Dep = x.ClockService

    interface Needs<ILogger> with
        member x.Dep = x.LoggerService

login
|> TaskFlow.run appRuntime CancellationToken.None
```

The important part is the contract, not the exact runtime shape.

## Testing stays small

For tests, implement only the caps the flow needs.

```fsharp
type LoginTestRuntime =
    { UserStoreService : IUserStore
      ClockService : IClock
      LoggerService : ILogger }

    interface LoginCaps with
        member x.UserStore = x.UserStoreService
        member x.Clock = x.ClockService
        member x.Logger = x.LoggerService

    interface Needs<IUserStore> with
        member x.Dep = x.UserStoreService

    interface Needs<IClock> with
        member x.Dep = x.ClockService

    interface Needs<ILogger> with
        member x.Dep = x.LoggerService
```

That keeps the test focused on the workflow, not on constructing an entire application shell.

## When not to use CAPS

Use CAPS when the dependency boundary matters.

Do not reach for them when:

- a private helper can just take a parameter
- the flow only needs a tiny local dependency and a named cap set would be noise
- you are tempted to hide service lookup behind `IServiceProvider`

For very small helpers, a direct generic constraint can still be fine. Keep the named cap set for public use-case boundaries.

## Source-Lifted Notes

The implementation matches the guide in two important ways:

- `flow {}`, `asyncFlow {}`, and `taskFlow {}` read `Env<'dep>` from any environment that implements `Needs<'dep>`
- projected requests like `Env<'dep, 'value>` reuse the existing bind/lift behavior for the projected value, so you do not need a separate capability API for each workflow family

That is why the same cap-set style works across `Flow`, `AsyncFlow`, and `TaskFlow`.

## Next

If you want the record-based version of the same idea, read [Environment Slicing](../ENV_SLICING).
If you want the broader workflow model, read [The FsFlow Model](../WHY_FSFLOW).
