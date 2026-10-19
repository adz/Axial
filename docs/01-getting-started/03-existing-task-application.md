---
title: Add Axial to an existing Task application
description: Adopt Flow in one module of a Task-based application without changing the host or the call sites above it.
---

# Add Axial to an existing Task application

Convert one module to `Flow` and turn it back into a `Task` at the boundary. Callers above it can stay on `Task`, and the host need not change.

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
        let! total = ColdTask(fun cancellationToken -> findTotal orderId cancellationToken)
        let! reference = ColdTask(fun cancellationToken -> charge total cancellationToken)
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

## Convert leaf functions last

Before binding a `CancellationToken -> Task<_>` function, wrap it in `ColdTask`. The wrapper keeps task creation cold
and receives Flow's runtime cancellation token.

When the task returns `Result<'value,'error>`, the builder sends `Error` to the expected-error channel.

Use `Flow.fromTask` or `Flow.fromTaskResult` when you compose without `flow { }`. Use `Flow.awaitStartedTask` only when
a host API has already started the operation.

Some legacy APIs do not accept a cancellation token. You can adapt one with `ColdTask(fun _ -> legacyCall ())`, but
Flow cancellation cannot stop the underlying operation.

Prefer cancellation-aware overloads. Add cancellation support to adapters that you control.

## Check effect boundaries during migration

Add `Axial.Guardrails` while you migrate modules to find dependencies that the existing signatures do not expose.
The analyzer detects ambient access to clocks, randomness, the console, and other operational effects.

The default warning severity does not fail an existing build. Resolve findings in each module as you migrate it,
then configure the analyzer to report errors when the project is clean.

For installation and configuration, see
[Installation](installation.html#add-effect-boundary-guardrails-optional).

## Related guides

- [Task and async interop](/the-flow-type/task-async-interop.html) covers the full set of conversions in both
  directions.
- [Expected errors and defects](/error-handling/index.html) explains which failures belong in `'error` and which
  belong in the defect channel.
- [Your first application](first-application.html) replaces the host entirely when a Flow is the application root.
- [Effect-boundary guardrails](/notes/guardrails.html) covers what the analyzer checks and how to mark an
  intended boundary.
