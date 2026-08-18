# Axial

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/content/img/axial-readme-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/content/img/axial-readme-light.svg">
  <img alt="Axial" src="docs/content/img/axial-readme-light.svg" width="160">
</picture>

Write asynchronous F# workflows whose expected failures and required dependencies are visible in their types.

[![ci](https://github.com/adz/Axial/actions/workflows/ci.yml/badge.svg)](https://github.com/adz/Axial/actions/workflows/ci.yml)
[![docs](https://github.com/adz/Axial/actions/workflows/livedocs.yml/badge.svg)](https://adz.github.io/Axial/)
[![NuGet](https://img.shields.io/nuget/v/Axial.svg)](https://www.nuget.org/packages/Axial)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

> [!WARNING]
> Axial is pre-1.0 and its API may change before the first stable release.

## Your first flow

A handler usually needs services and can fail, but a `Task` signature shows neither fact. `Flow<'env, 'error, 'value>`
makes both part of the contract.

```fsharp
open Axial

type CheckoutError =
    | OrderNotFound of orderId: int
    | PaymentDeclined of reason: string

type Receipt = { OrderId: int; Total: decimal; Reference: string }

type CheckoutEnv =
    { FindTotal: int -> ColdTask<Result<decimal, CheckoutError>>
      Charge: decimal -> ColdTask<Result<string, CheckoutError>> }

let checkout orderId : Flow<CheckoutEnv, CheckoutError, Receipt> =
    flow {
        let! findTotal = Flow.envWith _.FindTotal
        let! charge = Flow.envWith _.Charge
        let! total = findTotal orderId
        let! reference = charge total
        return { OrderId = orderId; Total = total; Reference = reference }
    }
```

The application supplies live functions. A test supplies a record of fakes. The workflow does not change.

Adding a timeout, a retry policy, and a resource that must be released does not change the signature either, because
the runtime that starts the workflow owns cancellation, retries, and cleanup:

```fsharp
open System

let checkoutOrder orderId : Flow<CheckoutEnv, CheckoutError, Receipt> =
    checkout orderId
    |> Flow.Runtime.retry (RetryPolicy.noDelay 3)
    |> Flow.Runtime.timeout (TimeSpan.FromSeconds 5.0) (PaymentDeclined "checkout timed out")
```

Flow also carries concurrency, scheduling, streams, and structured child fibers through the same runtime.

## Install

```bash
dotnet add package Axial
```

## Packages

The core is independent. Add service and hosting packages only when the workflow uses them.

- `Axial` — workflows, typed failures, dependencies, concurrency, schedules, streams, and layers
- `Axial.PlatformService` — explicit clock, logging, randomness, GUID, and environment services
- `Axial.Console`, `Axial.FileSystem`, `Axial.HttpClient`, `Axial.Process` — mockable operational services
- `Axial.Hosting`, `Axial.Hosting.Node`, `Axial.Hosting.Browser` — application lifecycle integrations
- `Axial.Telemetry` — tracing and runtime observability
- `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp` — optional adapters for serving Reified HTTP contracts

## Documentation and examples

- [Getting started](docs/01-getting-started/_index.md)
- [Add Axial to an existing Task application](docs/01-getting-started/03-existing-task-application.md)
- [Failures and defects](docs/04-error-handling/_index.md)
- [Dependencies and services](docs/05-dependencies/_index.md)
- [Concurrency](docs/08-concurrency-and-state/_index.md)
- [HTTP client](docs/06-services/05-http/_index.md)
- [Runnable examples](docs/13-testing/02-runnable-examples.md)
- [Integration reference application](examples/Axial.ReferenceApp/README.md)

## Reified integration

[Reified](https://github.com/adz/Reified) declares value, model, JSON, and HTTP contracts. Axial's optional server adapters execute Reified HTTP contracts as workflows; neither core depends on the other.

Declare a contract with Reified. Serve it with Axial.
