---
title: Providing Services from a Package
description: Authoring a library that asks callers for a dependency it cannot name.
---

# Providing Services from a Package

Application code owns both sides of its environment: the workflow names `AppEnv`, and the composition root supplies
one. A package author has neither. Your library is compiled before its callers exist, so it cannot mention their
types, and it should not force every consumer into one record shape.

This page is the authoring side of [service contracts](/dependencies/service-contracts.html). Everything Axial's own
service packages do, you can do.

## The shape

Three declarations per service, and the third is the only one with any subtlety.

**The service**: an ordinary interface describing the capability:

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
type IExchangeRates =
    abstract GetUsdToAud : unit -> Task<decimal>
```

**The contract**: how an environment advertises that it supplies one. Named `IHasFoo`, exposing exactly one member
`Foo`:

```fsharp
type IHasExchangeRates =
    abstract ExchangeRates : IExchangeRates
```

**The accessor**: one module-level binding that reads it:

```fsharp
[<RequireQualifiedAccess>]
module ExchangeRates =
    let service<'env, 'error when 'env :> IHasExchangeRates> : Flow<'env, 'error, IExchangeRates> =
        Flow.envWith _.ExchangeRates
```

Bind the accessor at module level, not inline. `Flow.envWith _.ExchangeRates` cannot resolve inside a `flow { }` block,
because the lambda's parameter type is not known until the surrounding annotation is applied, which happens after
the body is checked. At module level the annotation sits next to the expression that needs it, so it resolves once
and every caller binds it with no annotation at all.

Everything the package publishes then builds on the accessor:

```fsharp
let priceInAud<'env, 'error when 'env :> IHasExchangeRates> (usdAmount: decimal) : Flow<'env, 'error, decimal> =
    flow {
        let! rates = ExchangeRates.service
        let! rate = ColdTask(fun _ -> rates.GetUsdToAud())
        return usdAmount * rate
    }
```

Any environment that implements the contract can run it:

```fsharp
type RatesEnv =
    { Rates: IExchangeRates }
    interface IHasExchangeRates with
        member this.ExchangeRates = this.Rates
```

```fsharp run
priceInAud 10m
|> Flow.run { Rates = { new IExchangeRates with member _.GetUsdToAud() = Task.FromResult 1.5m } }
|> shouldEqual (Exit.Success 15.0m : Exit<decimal, string>)
```

## Rules that keep contracts composable

**One member per contract, named after the suffix.** `IHasFoo` exposes `Foo`. This is what makes `Flow.envWith _.Foo`
predictable and keeps a consumer's composition root readable when it implements six of them.

**Never inherit a generic interface.** F# rejects a type parameter constrained by two instantiations of the same
generic interface, so a generic parent makes your contract impossible to combine with any other, including one from
a different package. A contract inherits nothing, or inherits other plain contracts.

```fsharp no-check reason="Shows the definition to avoid beside the one to use; both have the same name"
type IHasRates = inherit IServiceContract<IExchangeRates>   // do not do this
type IHasRates = abstract ExchangeRates : IExchangeRates    // do this
```

**Member names may collide freely.** Two packages can both define `IHasClient` exposing `Client`, and one record can
implement both. F# interface implementations are always explicit, so there is no ambiguity, and package authors do
not need to coordinate names.

## Typed errors belong in the package

The reason to publish operations rather than just the interface is that you can wrap the failure model once. Compare
the raw interface call with what `Axial.FileSystem` publishes:

```fsharp no-check reason="Two calls side by side for their result types"
fileSystem.ReadAllText path                 // string, throws
FileSystem.readAllText path                 // Flow<'env, FileSystemError, string>
```

The second is the first plus `Flow.catch`, classifying exceptions into a union the caller can match on. That
translation is the package's job: the package does it once, and every consumer gets typed failures.

## Also expose the raw service

Publish the accessor (`ExchangeRates.service`) as part of the public API. Callers occasionally need the interface
itself for interop, and without it there is no way to reach it once the environment is contract-based. Axial's own
packages all do this.
