---
title: Add Axial to an existing Task application
description: Adopt Flow in one module of a Task-based application without changing the host or the call sites above it.
---

# Add Axial to an existing Task application

You do not have to convert an application to adopt Axial. A `Flow` becomes a `Task` at one boundary, so you can
convert a single module, keep every caller above it on `Task`, and leave the host untouched.

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
        | Exit.Failure cause -> return failwith (Cause.prettyPrint string cause)
    }
```

Calling `handleCheckout live 42 CancellationToken.None` returns `200 ch_1a2b3c`, and `handleCheckout live 7`
returns `404 order 7 not found`.

Three properties of that match matter:

- The compiler lists the `Cause.Fail` cases for you. Adding a case to `CheckoutError` makes every boundary that maps
  it incomplete, which is the point of putting failures in the type.
- The final case covers `Cause.Die` and `Cause.Interrupt`: defects and interruption, not expected failures. Re-raise
  or log them the way your host already handles unhandled exceptions.
- The host's cancellation token flows in, so cancelling the request cancels every inner call and closes the
  workflow's scope. `ColdTask` supplies that token to each task dependency without adding it to the workflow's public
  parameters.

## Keep the environment where the host already keeps services

The environment is a record, so build it once from whatever your host already resolves:

```fsharp no-check reason="The service provider and repositories belong to the reader's existing application"
let checkoutEnv (provider: IServiceProvider) : CheckoutEnv =
    let orders = provider.GetRequiredService<IOrderRepository>()
    let payments = provider.GetRequiredService<IPaymentGateway>()

    { FindTotal = fun orderId cancellationToken -> orders.FindTotalAsync(orderId, cancellationToken)
      Charge = fun total cancellationToken -> payments.ChargeAsync(total, cancellationToken) }
```

Register that function with your container and resolve `CheckoutEnv` in the handler. The workflows below the boundary
still take a plain record and stay testable without the container.

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

## Go further

- [Task and async interop](/the-flow-type/task-async-interop.html) covers the full set of conversions in both
  directions.
- [Expected errors and defects](/error-handling/index.html) explains which failures belong in `'error` and which
  belong in the defect channel.
- [Your first application](first-application.html) replaces the host entirely when a Flow is the application root.
- [Effect-boundary guardrails](/notes/guardrails.html) covers what the analyzer checks and how to mark an
  intended boundary.
