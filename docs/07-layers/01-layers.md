---
title: Composing Layers
description: Provisioning explicit environments with Layer and Layer.provide.
---

# Composing Layers

A `Layer<'input, 'error, 'output>` builds an environment or service bundle from an input value. It runs inside a
`Scope`, so resources acquired during provisioning can be finalized when the provided flow finishes.

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
open Axial
open Axial.Layers
```

The examples on this page provision an orders repository from a settings map. Opening the repository is recorded in
`events`, so each example can check what was opened and closed:

```fsharp
type Config = { ConnectionString: string }
type StartupError = MissingSetting of string

type IOrderRepository =
    abstract Count: unit -> int

type AppEnv =
    { Config: Config
      Orders: IOrderRepository
      Clock: IClock }

let events = ResizeArray<string>()

let fail (error: StartupError) : Layer<'input, StartupError, 'output> =
    Layer.fromAsync (fun _ _ -> async { return Exit.Failure(Cause.Fail error) })

let configLayer : Layer<Map<string, string>, StartupError, Config> =
    Layer.envWith (Map.tryFind "orders")
    |> Layer.bind (function
        | Some connectionString -> Layer.succeed { ConnectionString = connectionString }
        | None -> fail (MissingSetting "orders"))

let ordersLayerFromConfig (config: Config) : Layer<Map<string, string>, StartupError, IOrderRepository> =
    Layer.acquireRelease
        (Layer.fromAsync (fun _ _ ->
            async {
                events.Add $"opened {config.ConnectionString}"
                return Exit.Success { new IOrderRepository with member _.Count() = 3 }
            }))
        (fun _ _ ->
            events.Add $"closed {config.ConnectionString}"
            Task.CompletedTask)

let clockLayer : Layer<Map<string, string>, StartupError, IClock> =
    Layer.succeed (Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))

let countOrders : Flow<AppEnv, StartupError, string> =
    Flow.envWith (fun env -> $"{env.Orders.Count()} orders in {env.Config.ConnectionString}")
```

## Primary Shape

Use `layer { }` for application environment construction.

`let!` binds a layer's output to the name on its left. `do!` binds a layer returning `unit`, while `return!` uses
another layer as the block's result. Sibling `and!` bindings build independent layers in parallel.

```fsharp
let appLayer : Layer<Map<string, string>, StartupError, AppEnv> =
    layer {
        let! config = configLayer
        let! orders = ordersLayerFromConfig config
        and! clock = clockLayer
        return { Config = config; Orders = orders; Clock = clock }
    }
```

Here is the same block with the left- and right-hand types shown:

```fsharp
let appLayerAnnotated =
    layer {
        let! (config: Config) = (configLayer: Layer<Map<string, string>, StartupError, Config>)

        let! (orders: IOrderRepository) =
            (ordersLayerFromConfig config: Layer<Map<string, string>, StartupError, IOrderRepository>)

        and! (clock: IClock) = (clockLayer: Layer<Map<string, string>, StartupError, IClock>)

        return { Config = config; Orders = orders; Clock = clock }
    }
// Layer<Map<string, string>, StartupError, AppEnv>
```

`Layer.provide` builds the layer, runs the workflow in the result, and releases what the layer acquired:

```fsharp run
events.Clear()

countOrders
|> Layer.provide appLayer
|> Flow.run (Map [ "orders", "db://orders" ])
|> shouldEqual (Exit.Success "3 orders in db://orders")

List.ofSeq events |> shouldEqual [ "opened db://orders"; "closed db://orders" ]

countOrders
|> Layer.provide appLayer
|> Flow.run Map.empty
|> shouldEqual (Exit.Failure(Cause.Fail(MissingSetting "orders")))
```

Plain `let!` is sequential and dependent. Sibling `and!` bindings are independent and use `Layer.merge`, which provisions
branches in parallel through child scopes. An `and!` sibling cannot depend on a value introduced by another sibling;
that is an ordinary F# compile-time scope error, and the fix is to move it into a prior `let!`.

## Layer functions

| Function | Use |
| --- | --- |
| `Layer.succeed value` | A value that is already built |
| `Layer.envWith projection` | A value read from the layer's input |
| `Layer.fromAsync`, `Layer.fromTask`, `Layer.fromValueTask` | Construction that can fail or register cleanup |
| `Layer.acquireRelease acquire release` | A resource that lives as long as the provided flow |
| `Layer.map`, `Layer.mapError`, `Layer.widenError` | Change the output or the error type |
| `Layer.bind` | A step that depends on an earlier value, like `let!` |
| `Layer.zip` | Two layers in sequence |
| `Layer.zipPar`, `Layer.merge` | Two layers in parallel |
| `Layer.map2`, `Layer.map3` | Combine outputs without nested tuples |

Layer error types must match the flow error type. When provisioning steps use different errors, map them into one
startup error type with `Layer.mapError` before calling `Layer.provide`. A layer that cannot fail, such as
`BaseRuntime.live` or `Clock.layer`, has error type `Never`; `Layer.widenError` gives it any error type.

## zip, zipPar, And merge

`Layer.zip` provisions left then right, sequentially. Use it when ordering is intentional. `Layer.zipPar` provisions
both sides independently in parallel and returns a tuple. `Layer.merge` is the layer-domain name for `zipPar`; prefer
it when combining service bundles or environment fragments.

`Layer.merge` does not merge service contracts or synthesize a new environment type. It only provisions both sides and
returns their outputs, so build the final environment yourself:

```fsharp
let mergedLayer : Layer<Map<string, string>, StartupError, AppEnv> =
    Layer.merge configLayer clockLayer
    |> Layer.bind (fun (config, clock) ->
        ordersLayerFromConfig config
        |> Layer.map (fun orders -> { Config = config; Orders = orders; Clock = clock }))
```

```fsharp run
countOrders
|> Layer.provide mergedLayer
|> Flow.run (Map [ "orders", "db://replica" ])
|> shouldEqual (Exit.Success "3 orders in db://replica")
```

This keeps service requirements visible to people, the compiler, and LLMs. It also avoids ambiguous cases such as two
services with the same implementation type. If an application needs multiple instances of the same service shape, give
them named record fields or distinct nominal contracts rather than relying on tags.

## Cleanup

`Layer.provide` creates a root scope, builds the layer, runs the downstream flow, and closes the scope. Cleanup runs when
the layer fails, the downstream flow fails, or the downstream flow succeeds.

If a later step fails after an earlier one acquired a resource, the resource is released when the root scope closes:

```fsharp run
events.Clear()

let halfBuilt : Layer<Map<string, string>, StartupError, IOrderRepository * IClock> =
    Layer.zip (ordersLayerFromConfig { ConnectionString = "db://orders" }) (fail (MissingSetting "clock"))

Flow.ok 0
|> Layer.provide (halfBuilt |> Layer.map ignore)
|> Flow.run Map.empty
|> shouldEqual (Exit.Failure(Cause.Fail(MissingSetting "clock")))

List.ofSeq events |> shouldEqual [ "opened db://orders"; "closed db://orders" ]
```

Parallel composition uses parent-owned child scopes, so the same holds when one parallel branch fails after another
acquired resources. If both parallel branches fail, Axial preserves both failures as `Cause.Both (leftCause, rightCause)` rather than
discarding one side.
