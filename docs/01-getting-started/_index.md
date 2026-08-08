---
weight: 1
title: Getting Started
---

# Complete one transaction

Suppose registration loads a user, saves it, and can fail in either step. A plain `Task<unit>` signature hides both
the required operations and the failures the caller should handle. Start with an environment record and an error
type:

```fsharp
open System.Threading.Tasks
open Axial

type User = { Id: int; Name: string }

type RegistrationError =
    | UserNotFound
    | SaveFailed of string

type RegistrationEnv =
    { LoadUser: int -> Task<Result<User, RegistrationError>>
      SaveUser: User -> Task<Result<unit, RegistrationError>> }
```

Install the core package:

```bash
dotnet add package Axial
```

Write the transaction with `flow { }`:

```fsharp
let register userId : Flow<RegistrationEnv, RegistrationError, unit> =
    flow {
        let! loadUser = Flow.read _.LoadUser
        let! saveUser = Flow.read _.SaveUser
        let! user = loadUser userId
        return! saveUser user
    }
```

`Flow.read` selects a dependency from the environment. Binding a `Task<Result<_, RegistrationError>>` waits for the
task and keeps its `Error` in the workflow's expected-error channel.

Supply the live dependencies once and run the workflow:

```fsharp
let live =
    { LoadUser = loadUserFromDatabase
      SaveUser = saveUserToDatabase }

let completed = (register 42).ToTask(live)
```

The type now states the complete contract:

```fsharp
Flow<RegistrationEnv, RegistrationError, unit>
```

- `RegistrationEnv` is what the workflow needs.
- `RegistrationError` is what callers are expected to handle.
- `unit` is the successful value.

## Swap the boundary in a test

The workflow does not change when the implementation changes:

```fsharp
let fake =
    { LoadUser = fun id -> Task.FromResult(Ok { Id = id; Name = "Ada" })
      SaveUser = fun _ -> Task.FromResult(Ok ()) }

let result = (register 42).ToTask(fake)
```

No container or ambient service lookup is involved. The fake value satisfies the same environment type as the live
value.

## Continue

1. [Why use Flow?](why-flow.html)
2. [Installation and packages](installation.html)
3. [Creating and running flows](../the-flow-type/index.html)
4. [Expected errors and defects](../error-handling/index.html)
5. [Dependencies, services, and layers](../dependencies/index.html)
6. [Application lifetime and hosting](../platforms-and-hosting/index.html)
