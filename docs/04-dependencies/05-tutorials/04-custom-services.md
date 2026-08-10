---
title: "Tutorial: Creating Reusable Services"
description: Define your own named service contracts and consume them through IHas and Service.get.
---

# Tutorial: Creating Reusable Services

Use a custom service when several workflows should depend on the same named contract without being tied to one concrete app record field name.

## Define The Contract

```fsharp
open System.Threading.Tasks

type IExchangeRates =
    abstract GetUsdToAud : unit -> Task<decimal>
```

## Write A Reusable Helper

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
        let! rate = rates.GetUsdToAud()
        return usdAmount * rate
    }
```

This helper no longer cares whether the caller stores the service in `Rates`, `Runtime.ExchangeRates`, or any other field. It only needs `IHasExchangeRates`.

## Provide An App Environment

```fsharp
type AppEnv =
    { Rates: IExchangeRates
      Region: string }

    interface IHasExchangeRates with
        member this.ExchangeRates = this.Rates
```

## Use A Test Double

```fsharp
type FixedRates(rate: decimal) =
    interface IExchangeRates with
        member _.GetUsdToAud() = Task.FromResult rate
```

Now every workflow that depends on `IExchangeRates` can run against the same deterministic test implementation.

This is the main step from "an app record for one workflow" to "reusable helpers shared across workflows."
