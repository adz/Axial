---
title: Dependencies
description: Declare what a workflow needs, supply it at the edge, and manage scoped resources.
---

# Dependencies

Pass dependencies as ordinary function arguments until several workflows need the same ones and threading them
through unrelated callers becomes noise. Then use an environment.

**Then pass Flow a record.** A workflow states what it needs in its environment channel; you build that record and
hand it over when the workflow runs:

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

```fsharp
type User = { Id: int; Name: string }

type IUserStore =
    abstract Load: int -> Result<User, string>

type AppEnv = { Users: IUserStore }

let loadUser (id: int) : Flow<AppEnv, string, User> =
    flow {
        let! users = Flow.envWith _.Users
        return! users.Load id
    }
```

```fsharp run
let users =
    { new IUserStore with
        member _.Load id = if id = 1 then Ok { Id = 1; Name = "Ada" } else Error $"no user {id}" }

loadUser 1 |> Flow.run { Users = users } |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
```

Most applications need nothing more. There is no container, registration, or resolution step: a test supplies a
different record, with fakes in place of the live services.

Two further mechanisms build on it. **Contracts** let a *package* ask for a service without
knowing your record type; that is how `Console.writeLine` and the rest of the
[built-in services](/services/index.html) work, and how you would publish your own. **Layers** are for provisioning
that is itself effectful; see [layers](/layers/index.html), a separate package.

## In this section

1. [The environment](the-environment.html): what `'env` actually is, and the functions that read it.
2. [Choosing an approach](choosing-an-approach.html): arguments, records, contracts, and layers compared.
3. [Service contracts](service-contracts.html): how a package asks for a dependency it cannot name.
4. [Providing the environment](providing-the-environment.html): building the value at a host boundary.
5. [Tutorials](tutorials/index.html): the same material worked end to end.

For the services Axial already implements (the clock, console, file system, processes, and HTTP), see
[built-in services](/services/index.html).
