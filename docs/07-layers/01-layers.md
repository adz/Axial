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
    Layer.fromFlow (Flow.fail error)

let configLayer : Layer<Map<string, string>, StartupError, Config> =
    Layer.envWith (Map.tryFind "orders")
    |> Layer.bind (function
        | Some connectionString -> Layer.succeed { ConnectionString = connectionString }
        | None -> fail (MissingSetting "orders"))

let openOrders (config: Config) : Flow<Map<string, string>, StartupError, IOrderRepository> =
    Flow.delay (fun () ->
        events.Add $"opened {config.ConnectionString}"
        Flow.ok { new IOrderRepository with member _.Count() = 3 })

let closeOrders (config: Config) (_: IOrderRepository) : Flow<Map<string, string>, Never, unit> =
    Flow.delay (fun () ->
        events.Add $"closed {config.ConnectionString}"
        Flow.ok ())

let ordersLayerFromConfig (config: Config) : Layer<Map<string, string>, StartupError, IOrderRepository> =
    Layer.fromFlow (Flow.scopeAcquireRelease (openOrders config) (closeOrders config))

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

The left- and right-hand types can be annotated:

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

`layer { }` uses `let!` (via `Layer.bind`) and `and!` (via `Layer.merge`/`Layer.zipPar`) above; the full combinator
inventory — `succeed`, `envWith`, `fromFlow`, `acquireRelease`, `addFinalizer`, `map`/`mapError`/`widenError`,
`zip`, `zipPar`/`merge`, and `map2`/`map3` — is in the
[Layer API reference](/api/Axial.LayerModule.html).

Layer error types must match the flow error type. When provisioning steps use different errors, map them into one
startup error type with `Layer.mapError` before calling `Layer.provide`. A layer that cannot fail, such as
`BaseRuntime.live` or `Clock.layer`, has error type `Never`; `Layer.widenError` gives it any error type.

```fsharp
type OrdersError = MissingOrders of string
type ClockError = BrokenClock of string

type ProvisionError =
    | OrdersProblem of OrdersError
    | ClockProblem of ClockError

let ordersFrom (config: string option) : Layer<unit, OrdersError, int> =
    match config with
    | Some _ -> Layer.succeed 3
    | None -> Layer.fromFlow (Flow.fail (MissingOrders "orders"))

let clockFrom : Layer<unit, Never, IClock> =
    Layer.succeed (Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))

// Map the typed-error union into the shared union and widen the Never layer to the same error type.
let provision : Layer<unit, ProvisionError, int * IClock> =
    Layer.zip
        (ordersFrom None |> Layer.mapError OrdersProblem)
        (clockFrom |> Layer.widenError)
```

```fsharp run
Flow.ok 0
|> Layer.provide (provision |> Layer.map ignore)
|> Flow.run ()
|> shouldEqual (Exit.Failure(Cause.Fail(OrdersProblem(MissingOrders "orders"))))
```

## zip, zipPar, And merge

`Layer.zip` provisions left then right, sequentially. `Layer.zipPar` (its layer-domain name is `Layer.merge`)
provisions both sides independently in parallel and returns a tuple. `Layer.merge` does not synthesize a new
environment type; it only provisions both sides, so build the final environment yourself:

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

Named environment fields make service requirements explicit and avoid ambiguity when two services share an implementation type. If an application needs multiple instances of the same service shape, give
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

```fsharp
let failOrders : Layer<unit, ProvisionError, int> =
    Layer.fromFlow (Flow.fail (OrdersProblem(MissingOrders "orders")))

let failClock : Layer<unit, ProvisionError, IClock> =
    Layer.fromFlow (Flow.fail (ClockProblem(BrokenClock "clock")))
```

```fsharp run
Flow.ok 0
|> Layer.provide (Layer.merge failOrders failClock |> Layer.map ignore)
|> Flow.run ()
|> function
| Exit.Failure cause ->
    Cause.failures cause
    |> shouldEqual [ OrdersProblem(MissingOrders "orders"); ClockProblem(BrokenClock "clock") ]
| other -> failwithf "Expected a failure, got %A" other
```
