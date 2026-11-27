---
title: Add Axial to an existing Task application
description: Adopt Flow in one module of a Task-based application without changing the host or the call sites above it.
---

# Add Axial to an existing Task application

Convert one module to `Flow` and turn it back into a `Task` at the boundary. Callers above it can stay on `Task`, and the host need not change.

Inside the module, the code you already have keeps working. An `Async` binds directly in `flow { }`. A task-returning
function becomes a `ColdTask`, which receives the flow's cancellation token. A `Task<Result<_, _>>`, such as
FsToolkit.ErrorHandling's `TaskResult`, keeps its `Error` as a typed failure. `Flow.cancellationToken` reads the
token for any other API that takes one. An exception from any of them becomes a defect, not a typed error.

This page assumes you have read [Get started](index.html) and repeats its `checkout` workflow so the example below
compiles on its own.

## Choose the boundary

Put the boundary where a request, job, or message begins. For example, use a controller action, endpoint handler,
hosted service loop, or message consumer.

Above the boundary, callers continue to use `Task`. Below it, the module uses workflows.

Do not scatter boundaries through the call tree. Each boundary starts its own runtime with its own scope, so
converting a leaf function first gives you the costs of Flow and none of its cancellation or cleanup guarantees.

## Start the workflow and translate the exit

`StartAsTask` supplies the environment, accepts the caller's cancellation token, and returns a
`Task<Exit<'value, 'error>>`. Match on the exit to turn typed failures into whatever your host already returns:

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
open Axial
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open System.Threading
open System.Threading.Tasks
open Axial

type CheckoutError =
    | OrderNotFound of orderId: int
    | PaymentDeclined of reason: string

type Receipt = { OrderId: int; Total: decimal; Reference: string }

type CheckoutEnv =
    { FindTotal: int -> CancellationToken -> Task<Result<decimal, CheckoutError>>
      Charge: decimal -> CancellationToken -> Task<Result<string, CheckoutError>> }

let checkout orderId : Flow<CheckoutEnv, CheckoutError, Receipt> =
    flow {
        let! findTotal = Flow.envWith _.FindTotal
        let! charge = Flow.envWith _.Charge
        let! total =
            ColdTask(fun cancellationToken -> findTotal orderId cancellationToken)
            |> Flow.fromColdTaskResult
        let! reference =
            ColdTask(fun cancellationToken -> charge total cancellationToken)
            |> Flow.fromColdTaskResult
        return { OrderId = orderId; Total = total; Reference = reference }
    }

let handleCheckout (env: CheckoutEnv) (orderId: int) (cancellationToken: CancellationToken) : Task<string> =
    task {
        let running = (checkout orderId).StartAsTask(env, cancellationToken = cancellationToken)

        match! running with
        | Exit.Success receipt -> return $"200 {receipt.Reference}"
        | Exit.Failure(Cause.Fail(OrderNotFound id)) -> return $"404 order {id} not found"
        | Exit.Failure(Cause.Fail(PaymentDeclined reason)) -> return $"402 {reason}"
        | Exit.Failure Cause.Interrupt -> return raise (OperationCanceledException(cancellationToken))
        | Exit.Failure cause -> return raise (InvalidOperationException(Cause.prettyPrint string cause))
    }
```

With an environment whose order 42 exists and whose payments succeed, the handler returns the host's usual
responses:

```fsharp run
let sample =
    { FindTotal = fun orderId _ -> Task.FromResult(if orderId = 42 then Ok 19.95m else Error(OrderNotFound orderId))
      Charge = fun _ _ -> Task.FromResult(Ok "ch_1a2b3c") }

(handleCheckout sample 42 CancellationToken.None).Result |> shouldEqual "200 ch_1a2b3c"
(handleCheckout sample 7 CancellationToken.None).Result |> shouldEqual "404 order 7 not found"
```

The match handles expected errors, defects, and cancellation:

- The two `Cause.Fail` arms map the expected errors. The final catch-all also matches any new `CheckoutError` case,
  so adding one does **not** make this match incomplete; review boundary mappings whenever the error union changes.
- `Cause.Interrupt` becomes a cancelled task at this host boundary. The final arm reports other causes, including
  defects and compound failures, as an exception with the cause text. Adapt that arm to your host's exception policy;
  it does not preserve the original defect exception.
- The host's cancellation token flows in, so cancelling the request cancels every inner call and closes the
  workflow's scope. `ColdTask` supplies that token to each task dependency without adding it to the workflow's public
  parameters.

## Keep the environment where the host already keeps services

The environment is a record, so build it once from whatever your host already resolves:

```fsharp no-check reason="Uses Microsoft.Extensions.DependencyInjection, which the documentation build does not reference"
open Microsoft.Extensions.DependencyInjection

type IOrderRepository =
    abstract FindTotalAsync: orderId: int * cancellationToken: CancellationToken -> Task<Result<decimal, CheckoutError>>

type IPaymentGateway =
    abstract ChargeAsync: total: decimal * cancellationToken: CancellationToken -> Task<Result<string, CheckoutError>>

let checkoutEnv (provider: IServiceProvider) : CheckoutEnv =
    // GetRequiredService throws when a registration is missing, so a missing service fails here
    // instead of surfacing as a null dereference later.
    { FindTotal = fun orderId cancellationToken -> provider.GetRequiredService<IOrderRepository>().FindTotalAsync(orderId, cancellationToken)
      Charge = fun total cancellationToken -> provider.GetRequiredService<IPaymentGateway>().ChargeAsync(total, cancellationToken) }
```

Register that function with your container and resolve `CheckoutEnv` in the handler. The workflows below the boundary
still take a plain record and stay testable without the container.

```fsharp no-check reason="Uses Microsoft.Extensions.DependencyInjection, which the documentation build does not reference"
open Microsoft.Extensions.DependencyInjection

// Register the concrete services, then the environment factory that resolves them.
let configureServices (services: IServiceCollection) =
    services.AddSingleton<IOrderRepository, SqlOrderRepository>() |> ignore
    services.AddSingleton<IPaymentGateway, StripePaymentGateway>() |> ignore
    services.AddSingleton<CheckoutEnv>(checkoutEnv) |> ignore
```

`checkoutEnv` is itself the factory `IServiceProvider -> CheckoutEnv`, so `AddSingleton<CheckoutEnv>(checkoutEnv)`
registers it directly. The handler resolves the record and hands it to the same boundary function the checked example
above used:

```fsharp no-check reason="Uses Microsoft.Extensions.DependencyInjection, which the documentation build does not reference"
let handleOrder (provider: IServiceProvider) (orderId: int) (token: CancellationToken) : Task<string> =
    let env = provider.GetRequiredService<CheckoutEnv>()
    handleCheckout env orderId token
```

## Bind the code you already have

Leaf functions keep their `Task` and `Async` signatures. Each kind binds into a flow without a rewrite:

```fsharp
/// An existing Async function.
let loadExchangeRate (currency: string) : Async<decimal> =
    async { return if currency = "EUR" then 0.92m else 1.0m }

/// An existing Task<Result<_, _>> function, the shape FsToolkit.ErrorHandling calls TaskResult.
let findOrderTotal (orderId: int) (cancellationToken: CancellationToken) : Task<Result<decimal, CheckoutError>> =
    Task.FromResult(if orderId = 42 then Ok 19.95m else Error(OrderNotFound orderId))

/// An existing .NET API that takes a token.
let recordQuote (orderId: int) (amount: decimal) (cancellationToken: CancellationToken) : Task<string> =
    Task.FromResult $"quote-{orderId}-{amount}"

let quote orderId : Flow<unit, CheckoutError, string> =
    flow {
        // Async binds directly.
        let! rate = loadExchangeRate "EUR"

        // ColdTask creates the task when the flow runs and hands it the flow's token;
        // Error from the Result becomes a typed failure.
        let! total =
            ColdTask(fun cancellationToken -> findOrderTotal orderId cancellationToken)
            |> Flow.fromColdTaskResult

        // Any other call that takes a token: read the flow's token and pass it on.
        let! cancellationToken = Flow.cancellationToken
        return! Flow.awaitStartedTask (recordQuote orderId (total * rate) cancellationToken)
    }
```

```fsharp run
quote 42 |> Flow.run () |> shouldEqual (Exit.Success "quote-42-18.3540")
quote 7 |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail(OrderNotFound 7)))
```

Nothing above `quote` passes a token: the runtime owns it. Cancelling the run, a `Flow.timeout` around it, or a lost
`Flow.race` cancels that token, so the .NET calls stop too.

Prefer `ColdTask` (or `Flow.fromTask` / `Flow.fromTaskResult` outside `flow { }`): it creates the task only when the
flow runs, so a retry starts it again. Use `Flow.awaitStartedTask` only for a task that is already running.

An exception from foreign code is a defect. It does not become a `CheckoutError`, so a typed handler cannot mistake a
crash for an expected failure:

```fsharp
let crashingLookup : Flow<unit, CheckoutError, decimal> =
    Flow.fromTask (fun _ -> Task.FromException<decimal>(InvalidOperationException "database unavailable"))
```

```fsharp run
match crashingLookup |> Flow.run () with
| Exit.Failure cause -> Cause.defects cause |> List.map _.Message |> shouldEqual [ "database unavailable" ]
| Exit.Success _ -> failwith "expected a defect"
```

[Failures and defects](/error-handling/index.html) explains the difference and how to recover from each.

Some legacy APIs do not accept a cancellation token. You can adapt one with `ColdTask(fun _ -> legacyCall ())`, but
Flow cancellation cannot stop the underlying operation. Prefer cancellation-aware overloads, and add cancellation
support to adapters you control.

## Add Guardrails when you are ready

`Axial.Guardrails` is optional; nothing on this page needs it. Once a module is converted, it finds what the old
signatures hid: a raw clock read, a `Task` nothing owns, an exception raised inside `flow { }`. Its default build
check only advises and never fails a build, so you can add it to an existing codebase and fix each module as you
convert it. See [Get started with Guardrails](/guardrails/getting-started.html) and
[Adopt with a baseline](/guardrails/baseline.html).

## Related guides

- [Task and async interop](/the-flow-type/task-async-interop.html) covers the full set of conversions in both
  directions.
- [Expected errors and defects](/error-handling/index.html) explains which failures belong in `'error` and which
  belong in the defect channel.
- [Your first application](first-application.html) replaces the host entirely when a Flow is the application root.
- [Guardrails](/guardrails/index.html) covers what the analyzer checks and how to mark an intended boundary.
