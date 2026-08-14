---
title: "Tutorial: Creating Reusable Services"
description: Define your own named service contracts and consume them through IHas and Service.get.
---

# Tutorial: Creating Reusable Services

The built-in services all follow one shape: a narrow interface (`IClock`, `IFileSystem`), an `IHasX` marker the
environment implements, and a module of helpers constrained by that marker rather than by any particular field
name. Nothing about that shape is special to the library — it is an ordinary pattern, and this tutorial builds one
for a service Axial does not ship: a currency conversion rate.

Reach for it when several workflows should depend on the same named contract without being tied to one concrete
app record field name — the same reason [built-in services](/services/index.html) are declared as `IHasX` instead
of read from a fixed field. A dependency used by exactly one workflow usually does not need this; see
[choosing an approach](/dependencies/choosing-an-approach.html).

## Define the contract

```fsharp
open System.Threading.Tasks

type IExchangeRates =
    abstract GetUsdToAud : unit -> Task<decimal>
```

Keep it as narrow as the built-in ones are. `IExchangeRates` exposes one conversion, not a general-purpose pricing
client — a workflow that needs more asks for more, the same way `IHasClock` does not also expose scheduling.

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
field. It only needs `IHasExchangeRates` — the same generic-constraint pattern `Clock.now` and
`FileSystem.readAllText` use.

## Give it a typed failure

`priceInAud` above lets a failed lookup surface as an unhandled `Task` exception, which is a defect, not something
a caller can react to. Most services worth naming this way are worth failing this way too — compare
[`FileSystemError`](/services/filesystem.html#typed-errors) or `HttpError`:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
type ExchangeRateError =
    | RateUnavailable of pair: string
    | ProviderTimedOut

let priceInAud<'env>
    (usdAmount: decimal)
    : Flow<'env, ExchangeRateError, decimal> when 'env :> IHasExchangeRates =
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

## Combine it with the built-in services

A custom service composes into the same environment as `BaseRuntime` exactly the way two built-in services do —
each gets its own interface member, delegating to wherever the value actually lives. See
[Tutorial: Composing Built-in Services](/services/existing-services.html) for the `BaseRuntime` half of this:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
open Axial.PlatformService

type AppEnv =
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

Now every workflow that depends on `IExchangeRates` can run against the same deterministic test implementation,
whether it is running alone or as part of the full `AppEnv` above.

## Publish it from a package

Everything on this page lives in the application. When the contract, the helper module, and a `live`
implementation should ship to callers you will never see — the same relationship `Axial.FileSystem` has to
`Axial.Core` — see [providing services from a package](reusable-packages.html) for the composable shape that
requires.

This is the main step from "an app record for one workflow" to "reusable helpers shared across workflows."
