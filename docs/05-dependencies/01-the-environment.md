---
title: The Environment
description: What the 'env parameter actually is, and the handful of functions that read it.
---

# The Environment

There is no container and no registration step. `'env` is an ordinary type parameter, and the value you supply is an
ordinary value.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp transcript
> (Flow.envWith (fun environment -> environment * 2) : Flow<int, Never, int>) |> Flow.run 21;;
val it: Exit<int,Never> = Success 42
```

The environment here is an `int`. `Flow` does not require a record, an interface, or a service. It hands your function
whatever value you passed to `Flow.run`.

## What the functions do

`Flow.envWith` **runs a function against the environment** and continues with the result. `_.Name` is F# shorthand
for `fun environment -> environment.Name`, so `Flow.envWith _.Name` is the same thing written shorter:

```fsharp transcript
> type Person = { Name: string; Age: int };;
> (Flow.envWith _.Name : Flow<Person, Never, string>) |> Flow.run { Name = "Ada"; Age = 36 };;
val it: Exit<string,Never> = Success "Ada"
```

The other environment functions:

| Function | What it does |
| --- | --- |
| `Flow.envWith projection` | Runs `projection` against the environment, continues with its result |
| `Flow.env` | Continues with the environment value itself |
| `Flow.localEnv change` | Runs a flow against a *different* environment computed by `change` |

`Flow.localEnv` is how a workflow needing a small environment runs inside one that has more:

```fsharp transcript
> type Person = { Name: string; Age: int };;
> let nameLength () : Flow<string, Never, int> = Flow.envWith (fun name -> name.Length);;
> nameLength () |> Flow.localEnv (fun (person: Person) -> person.Name) |> Flow.run { Name = "Ada"; Age = 36 };;
val it: Exit<int,Never> = Success 3
```

## What you will actually use

An `int` proves the point but is not the shape you want. In practice the environment is **a record you define**,
holding one field per dependency:

```fsharp
type User = { Id: int; Name: string }

type IUserStore =
    abstract Load: int -> Result<User, string>

type IAuditLog =
    abstract Record: string -> unit

type AppEnv =
    { Users: IUserStore
      Audit: IAuditLog }

let loadUser (id: int) : Flow<AppEnv, string, User> =
    flow {
        let! users = Flow.envWith _.Users
        let! audit = Flow.envWith _.Audit
        let! user = users.Load id
        audit.Record $"loaded {id}"
        return user
    }
```

You construct that record in exactly two places:

- **At boot**, with the live implementations, for example `{ Users = SqlUserStore(connection); Audit = FileAuditLog(path) }`.
- **In tests**, with fakes: the same record type with different values.

```fsharp
type InMemoryUsers(users: User list) =
    interface IUserStore with
        member _.Load id =
            match users |> List.tryFind (fun user -> user.Id = id) with
            | Some user -> Ok user
            | None -> Error $"no user {id}"

type ListAudit() =
    let entries = ResizeArray<string>()
    member _.Entries = List.ofSeq entries

    interface IAuditLog with
        member _.Record entry = entries.Add entry
```

```fsharp run
let audit = ListAudit()
let underTest = { Users = InMemoryUsers [ { Id = 42; Name = "Ada" } ]; Audit = audit }

loadUser 42 |> Flow.run underTest |> shouldEqual (Exit.Success { Id = 42; Name = "Ada" })
loadUser 7 |> Flow.run underTest |> shouldEqual (Exit.Failure(Cause.Fail "no user 7"))
audit.Entries |> shouldEqual [ "loaded 42" ]
```

Larger systems often define one record per architectural boundary rather than a single application-wide one, and use
`Flow.localEnv` to move between them. A billing subsystem that cannot see the mailer is a record without a mailer
field, and that is enforced by the compiler rather than by convention.

## Where the rest of the section goes

The rest of this section covers the cases a plain record does not:

- [Choosing an approach](choosing-an-approach.html): when arguments beat a record, and when a record stops being
  enough.
- [Service contracts](service-contracts.html): how a *package* asks for a dependency without knowing your record
  type.
- [Providing the environment](providing-the-environment.html): building the value at a host boundary.
