---
title: "Tutorial: Creating Reusable Services"
description: Define your own named service contracts and consume them through IHas and Service.get.
---

# Tutorial: Creating Reusable Services

Built-in services use a narrow interface (`IClock`, `IFileSystem`), an `IHasX` marker the environment implements,
and helpers constrained by that marker rather than by a field name. This tutorial uses the same shape for a
currency conversion rate.

Use a named contract when several workflows need the same dependency without relying on one app record field name.
[Built-in services](/services/index.html) use `IHasX` for this reason. A dependency used by exactly one workflow
usually does not need a contract; see [choosing an approach](/dependencies/choosing-an-approach.html).

## Define the contract

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
open System.Threading.Tasks

type IExchangeRates =
    abstract GetUsdToAud : unit -> Task<decimal>
```

Make it as narrow as the built-in ones. `IExchangeRates` exposes one conversion, not a general-purpose pricing
client; a workflow that needs more asks for more, the same way `IHasClock` does not also expose scheduling.

## Write a reusable helper

```fsharp
type IHasExchangeRates =
    abstract ExchangeRates : IExchangeRates

[<RequireQualifiedAccess>]
module ExchangeRates =
    let service<'env, 'error when 'env :> IHasExchangeRates> : Flow<'env, 'error, IExchangeRates> =
        Flow.envWith _.ExchangeRates

let priceInAud<'env, 'error when 'env :> IHasExchangeRates>
    (usdAmount: decimal)
    : Flow<'env, 'error, decimal> =
    flow {
        let! rates = ExchangeRates.service
        let! rate = ColdTask(fun _ -> rates.GetUsdToAud())
        return usdAmount * rate
    }
```

This helper no longer cares whether the caller stores the service in `Rates`, `Runtime.ExchangeRates`, or any other
field. It only needs `IHasExchangeRates`, the same generic-constraint pattern `Clock.now` and
`FileSystem.readAllText` use.

## Give it a typed failure

`priceInAud` above lets a failed lookup surface as an unhandled `Task` exception, which is a defect, not something
a caller can react to. For typed failures, compare
[`FileSystemError`](/services/filesystem.html#typed-errors) or `HttpError`:

```fsharp
type ExchangeRateError =
    | RateUnavailable of pair: string
    | ProviderTimedOut

let priceInAudOrFail<'env when 'env :> IHasExchangeRates>
    (usdAmount: decimal)
    : Flow<'env, ExchangeRateError, decimal> =
    flow {
        let! rates = ExchangeRates.service
        let! rate =
            Flow.attemptTask (fun _ -> rates.GetUsdToAud())
            |> Flow.mapError (fun _ -> ProviderTimedOut)
        return usdAmount * rate
    }
```

Now a caller can match `RateUnavailable` or `ProviderTimedOut` as ordinary values instead of catching an exception.

## Provide an app environment

```fsharp
type AppEnv =
    { Rates: IExchangeRates
      Region: string }

    interface IHasExchangeRates with
        member this.ExchangeRates = this.Rates
```

```fsharp run
let fixedRate = { new IExchangeRates with member _.GetUsdToAud() = Task.FromResult 1.5m }
let failing = { new IExchangeRates with member _.GetUsdToAud() = Task.FromException<decimal>(TimeoutException()) }

priceInAud 10m |> Flow.run { Rates = fixedRate; Region = "au" } |> shouldEqual (Exit.Success 15.0m : Exit<decimal, string>)
priceInAudOrFail 10m |> Flow.run { Rates = failing; Region = "au" } |> shouldEqual (Exit.Failure(Cause.Fail ProviderTimedOut))
```

## Combine it with the built-in services

A custom service and `BaseRuntime` can share an environment: each contract gets its own interface member,
delegating to the stored value. See
[Tutorial: Composing Built-in Services](/services/existing-services.html) for the `BaseRuntime` half of this:

```fsharp
type FullAppEnv =
    { Runtime: BaseRuntime
      Rates: IExchangeRates }

    interface IHasClock with
        member this.Clock = this.Runtime.Clock

    interface IHasExchangeRates with
        member this.ExchangeRates = this.Rates
```

## Use a test double

```fsharp
type FixedRates(rate: decimal) =
    interface IExchangeRates with
        member _.GetUsdToAud() = Task.FromResult rate
```

Workflows that depend on `IExchangeRates` can use this deterministic test implementation alone or as part of the
full `AppEnv` above.

## Publish it from a package

The examples above define the service in the application. To ship the contract, helper module, and a `live`
implementation to other applications (as `Axial.FileSystem` does for `Axial.Core`), see
[providing services from a package](reusable-packages.html).
