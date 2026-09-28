---
title: Getting Started
description: Run a tiny Flow, then call one real application service with its dependency and failure cases in the type.
---

# Get started

## Before you begin

Install the [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or later, then install Axial:

```bash
dotnet add package Axial
```

## The smallest Flow

Try it in F# Interactive (`dotnet fsi`, after `#r "nuget: Axial";;` and `open Axial;;`):

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
> let ready () : Flow<string> = Flow.succeed "The application is ready.";;
> Flow.run () (ready ());;
val it: Exit<string,Never> = Success "The application is ready."
```

`ready` describes work; it has not started. `Flow.run ()` is the edge that starts it. `Flow<string>` is the short
spelling for a Flow with no capabilities and no expected failure: `Flow<unit, Never, string>`. The
[aliases table](../the-flow-type/flow-type.html#aliases) lists every short form and what it expands to.

<div class="flow-type-diagram">
<img class="flow-type-diagram--light" data-theme-variant="light" src="../content/img/flow-type-light.svg" alt="Flow env error value: environment is the services it needs, error is expected failure, and value is the successful result." />
<img class="flow-type-diagram--dark" data-theme-variant="dark" src="../content/img/flow-type-dark.svg" alt="Flow env error value: environment is the services it needs, error is expected failure, and value is the successful result." />
</div>

Here, `unit` means no capabilities and `Never` means no expected failure. A capability is a value the workflow is
allowed to use: usually a service dependency, but sometimes configuration or request context. Only `string` carries
information here, so the alias keeps the first signature uncluttered.

You do not need to carry those two empty slots around until the workflow needs them. The next example does.

## A useful Flow: quote a price

Suppose the application already has an exchange-rate service. The service is ordinary application code: its live
implementation might call an API, use a cache, or dispatch to another service. Axial does not construct it and does
not need to know how it works.

The workflow names the one service it needs and turns the service's cancellable `Task<Result<_, _>>` operation into a
Flow:

```fsharp
type QuoteError =
    | RateUnavailable

type IExchangeRates =
    abstract UsdToAud: CancellationToken -> Task<Result<decimal, QuoteError>>

type QuoteApp = { ExchangeRates: IExchangeRates }

let quoteAud (usd: decimal) : Flow<QuoteApp, QuoteError, decimal> =
    flow {
        let! rates = Flow.envWith _.ExchangeRates
        let! rate = ColdTask rates.UsdToAud
        return Math.Round(usd * rate, 2)
    }
```

The type reads as a contract: `quoteAud` needs `QuoteApp`, can fail with `QuoteError`, and otherwise returns a decimal.
The caller does not pass a cancellation token; `ColdTask` receives the one owned by the Flow runtime and gives it to
the service.

Where the workflow runs, build the `QuoteApp` record. Here the service is a small implementation with a fixed rate,
constructed directly:

```fsharp
type FixedRate(rate: Result<decimal, QuoteError>) =
    interface IExchangeRates with
        member _.UsdToAud _ = Task.FromResult rate
```

```fsharp run
quoteAud 80m
|> Flow.run { ExchangeRates = FixedRate(Ok 1.52m) }
|> shouldEqual (Exit.Success 121.60m)

quoteAud 80m
|> Flow.run { ExchangeRates = FixedRate(Error RateUnavailable) }
|> shouldEqual (Exit.Failure(Cause.Fail RateUnavailable))
```

If the application already registers its services with a dependency-injection container (an `IServiceProvider`),
fill the record from it instead:

```fsharp
let quoteApp (services: IServiceProvider) : QuoteApp =
    { ExchangeRates = services.GetService typeof<IExchangeRates> :?> IExchangeRates }
```

With `Microsoft.Extensions.DependencyInjection` opened, `services.GetRequiredService<IExchangeRates>()` does the same
and reports a missing registration clearly.

The host's container supplies the `IServiceProvider`. For this example, a one-service provider stands in for it:

```fsharp run
let services =
    { new IServiceProvider with
        member _.GetService serviceType =
            if serviceType = typeof<IExchangeRates> then box (FixedRate(Ok 1.52m)) else null }

quoteAud 80m |> Flow.run (quoteApp services) |> shouldEqual (Exit.Success 121.60m)
```

Either way, each area of the application gets a record holding only the services it uses. `quoteAud` sees `QuoteApp`
and nothing else, even when the host registers dozens of services, so the record marks the edge of that area. A test
builds the same record with its own `IExchangeRates`, as `FixedRate` does here, and the workflow code does not change.

## What's next

1. [Why Flow?](why-flow.html) explains when this model earns its cost and when `Result` or `Task` is still the
   right answer.
2. [Installation and packages](installation.html) covers the package map.
3. [Add Axial to an existing Task application](existing-task-application.html) shows the one-module adoption path.
4. [Your first application](first-application.html) runs a Flow as an application root.
5. [Creating and running flows](../the-flow-type/index.html) covers every way to create and run a flow.
6. [Expected errors and defects](../error-handling/index.html) explains the error channel and defects.
7. [Dependencies, services, and layers](../dependencies/index.html) scales the environment record up.
