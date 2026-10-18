---
title: Troubleshooting Types
---

# Troubleshooting Types

These compiler errors often indicate a mismatch between wrapper types.

## Error: A Flow Alias Does Not Match The Channels You Intended

`Flow<'env, 'error, 'value>` is the full workflow shape. The shorter aliases remove common channels; the complete
alias table, with what each expansion means, is in [Reading the type](/the-flow-type/flow-type.html#aliases).
If your workflow reads an environment and has a typed domain error, use the full `Flow<'env, 'error, 'value>` form.

```fsharp prepare
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

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

## FS0030: Value Restriction On A Generic Flow

A value cannot be generic over an environment type. This declaration fails with FS0030 (the value restriction):

```fsharp no-check reason="FS0030: a generic value cannot be generalized"
type Settings = { Name: string }
let load : Flow<'env, Never, Settings> when 'env :> IHasFileSystem =
    Flow.ok { Name = "default" }
```

Make `load` a type function instead:

```fsharp
type Settings = { Name: string }
let loadSettings<'env when 'env :> IHasFileSystem> : Flow<'env, Never, Settings> =
    Flow.ok { Name = "default" }
```

```fsharp run
let env = { new IHasFileSystem with member _.FileSystem = TestFileSystem() :> IFileSystem }
loadSettings<IHasFileSystem> |> Flow.run env |> shouldEqual (Exit.Success { Name = "default" })
```

## Error: A Unique Overload For Method `Bind` Could Not Be Determined

The compiler cannot determine which wrapper shape a `let!` value should use.

Example:

```fsharp
let nested : Async<Async<Result<int, string>>> =
    async {
        return async { return Ok 42 }
    }

let workflow : Flow<unit, string, int> =
    flow {
        let! next = nested
        let! value = next
        return value
    }
```

The second `let!` is ambiguous.

Fix it with a type annotation:

```fsharp
let annotated : Flow<unit, string, int> =
    flow {
        let! next = nested
        let! (value: int) = next
        return value
    }
```

```fsharp run
annotated |> Flow.run () |> shouldEqual (Exit.Success 42)
```

## Error: The Flow Requires A Different Environment Type

A workflow written against one environment type (or service contract) cannot run directly inside a different environment type.

Example 1: Records

```fsharp
type SmallEnv = { Prefix: string }
type BigEnv = { App: SmallEnv; RequestId: string }

let greet : Flow<SmallEnv, string, string> =
    flow {
        let! prefix = Flow.envWith _.Prefix
        return $"{prefix} world"
    }

// Run in BigEnv using localEnv
let greetInBigEnv : Flow<BigEnv, string, string> =
    greet |> Flow.localEnv _.App
```

```fsharp run
greetInBigEnv
|> Flow.run { App = { Prefix = "hello" }; RequestId = "r-1" }
|> shouldEqual (Exit.Success "hello world")
```

Example 2: Services

If a helper requires `IHasDatabase` but you are running it in an environment that doesn't implement it, the compiler will error.

```fsharp no-check reason="Does not compile when AppEnv lacks IHasDatabase; that is the error this section explains"
let helper : Flow<#IHasDatabase, _, _> = ...

// This fails if AppEnv doesn't implement IHasDatabase
let run (env: AppEnv) = helper |> Flow.startTask env
```

Fix it by implementing the interface on your environment type.


## Error: `Option` Or `ValueOption` Does Not Match Your Error Type

Implicit option binding only works when the workflow error type is `unit`.

This fails:

```fsharp no-check reason="Does not compile; that is the error this section explains"
let workflow : Flow<unit, string, int> =
    flow {
        let! value = Some 42
        return value
    }
```

Use an explicit adapter when you want a custom error:

```fsharp
let optionWorkflow : Flow<unit, string, int> =
    Some 42
    |> Flow.fromOption "missing value"
```

```fsharp run
optionWorkflow |> Flow.run () |> shouldEqual (Exit.Success 42)
(None |> Flow.fromOption "missing value" : Flow<unit, string, int>) |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail "missing value"))
```

## Error: Task is not a Flow builder source

Raw `Task<'value>` and `ValueTask<'value>` values do not bind directly in `flow { }`. A task is already running, while
Flow is a cold description that may run more than once.

Wrap work that should start when the Flow runs in `ColdTask`:

```fsharp
let loadAsync (cancellationToken: CancellationToken) : Task<int> =
    task {
        do! Task.Delay(1, cancellationToken)
        return 42
    }

let load : ColdTask<int> = ColdTask loadAsync

let coldWorkflow : Flow<int> =
    flow {
        let! value = load
        return value
    }
```

```fsharp run
coldWorkflow |> Flow.run () |> shouldEqual (Exit.Success 42)
```

If the cold task returns `Result<'value,'error>`, `let!` and `return!` place `Error` in Flow's typed error channel.

When work has already started, name that lifecycle explicitly:

```fsharp
let runningTask : Task<int> = loadAsync CancellationToken.None

let startedWorkflow : Flow<int> =
    flow {
        let! value = Flow.awaitStartedTask runningTask
        return value
    }
```

```fsharp run
startedWorkflow |> Flow.run () |> shouldEqual (Exit.Success 42)
```

Use `Flow.awaitStartedTaskResult` when the started task returns `Result`.

## Flexible type variables (`#T`) are each distinct

`#IHasOrders` is F# shorthand for `'env when 'env :> IHasOrders`, a fresh type variable with one constraint. Each
occurrence of `#T` in a signature is a separate type variable, so two occurrences do not unify to the same
environment unless you name the variable explicitly.

```fsharp no-check reason="Illustrates the aggregate-contract pattern from the service contracts guide"
type ICheckoutEnv =
    inherit IHasOrders
    inherit IHasEmail

let submitOrder (order: Order) : Flow<#ICheckoutEnv, CheckoutError, unit> = submit order
```

When the same environment appears more than once — two inputs, or an input and an output — write the explicit form
so all occurrences name one variable:

```fsharp
type IHasOrders =
    abstract Orders: unit

let sequence<'env when 'env :> IHasOrders> (a: Flow<'env, 'e, 'v>) (b: Flow<'env, 'e, 'v>) : Flow<'env, 'e, 'v> =
    Flow.bind (fun _ -> b) a
```

See [service contracts](/dependencies/service-contracts.html) for when to declare one.

## Check wrapper boundaries

If the compiler error mentions one of these shapes, check the boundary first:

- `Result<...>`
- `Async<...>`
- `Async<Result<...>>`
- `Task<...>`
- `Task<Result<...>>`
- `Flow<...>`

`Flow.retry` and `Flow.repeat` take a `Schedule`. If the compiler reports a mismatch on the schedule's input type, check that the schedule's input matches the flow's error type (for `retry`) or value type (for `repeat`).

To resolve the mismatch:

- add a type annotation to disambiguate `let!` overloads
- derive a smaller local environment with `localEnv`
- use [`Bind.error` or `Bind.mapError`](/error-handling/bind.html) at a `flow { }` bind site when the source error must
  be assigned or mapped first
- move back to plain Result until the real workflow boundary appears
