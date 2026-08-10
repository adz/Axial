---
title: Getting Started
description: Run a complete Axial workflow, then add the timeout, retry, and cleanup that plain Task code has to hand-roll.
---

# Get started

This page has one program in it. Install the package, paste the program into a script, and run it. Everything after
that adds one requirement at a time to the same program.

## Before you begin

Install the [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or later, then install Axial:

```bash
dotnet add package Axial
```

`Axial` is the package. `Flow<'env, 'error, 'value>` is the type it gives you: a description of asynchronous work,
the environment it reads, and the failure it can produce.

## Run your first workflow

Put this in a project that references `Axial`, or save it as `checkout.fsx` with `#r "nuget: Axial"` as the first
line and run `dotnet fsi checkout.fsx`:

```fsharp
open System.Threading.Tasks
open Axial

type CheckoutError =
    | OrderNotFound of orderId: int
    | PaymentDeclined of reason: string

type Receipt = { OrderId: int; Total: decimal; Reference: string }

type CheckoutEnv =
    { FindTotal: int -> Task<Result<decimal, CheckoutError>>
      Charge: decimal -> Task<Result<string, CheckoutError>> }

let checkout orderId : Flow<CheckoutEnv, CheckoutError, Receipt> =
    flow {
        let! findTotal = Flow.envWith _.FindTotal
        let! charge = Flow.envWith _.Charge
        let! total = findTotal orderId
        let! reference = charge total
        return { OrderId = orderId; Total = total; Reference = reference }
    }

let live =
    { FindTotal =
        fun orderId ->
            if orderId = 42 then
                Task.FromResult(Ok 19.99m)
            else
                Task.FromResult(Error(OrderNotFound orderId))
      Charge = fun _ -> Task.FromResult(Ok "ch_1a2b3c") }

let report orderId =
    match checkout orderId |> Flow.run live with
    | Exit.Success receipt -> printfn $"paid %.2f{receipt.Total} for order {receipt.OrderId} ({receipt.Reference})"
    | Exit.Failure cause -> printfn $"{Cause.prettyPrint string cause}"

report 42
report 7
```

The output is:

```text
paid 19.99 for order 42 (ch_1a2b3c)
Fail(OrderNotFound 7)
```

Three things happened in that program:

- `Flow.envWith` selected a dependency from the environment. The workflow never constructs its dependencies and never
  looks them up in a container.
- Binding a `Task<Result<_, CheckoutError>>` awaited the task and routed its `Error` into the workflow's
  expected-error channel, so the happy path stayed unnested.
- `Flow.run` supplied the environment at one boundary and returned an `Exit` that is either a success value or a
  `Cause`.

The signature states the whole contract. `CheckoutEnv` is what the workflow needs, `CheckoutError` is what callers
must handle, and `Receipt` is the success value.

## Add the requirements that plain Task makes you hand-roll

A real checkout has more rules than the version above: hold a database connection, give up after five seconds, and
retry a declined payment a few times but never retry a missing order.

Written against `Task`, each rule is a separate mechanism, and the signature records none of them:

```fsharp no-check reason="Contrasting Task-based sketch; its database and payment APIs are not part of Axial"
let checkout (cancellationToken: CancellationToken) (services: AppServices) orderId =
    task {
        use connection = services.OpenConnection()
        use timeoutSource = CancellationTokenSource.CreateLinkedTokenSource cancellationToken
        timeoutSource.CancelAfter(TimeSpan.FromSeconds 5.0)

        let mutable attempt = 1
        let mutable result = Unchecked.defaultof<Result<Receipt, CheckoutError>>
        let mutable finished = false

        while not finished do
            match! chargeOnce connection timeoutSource.Token orderId with
            | Error(PaymentDeclined _) when attempt < 3 ->
                do! Task.Delay(100 * attempt, timeoutSource.Token)
                attempt <- attempt + 1
            | outcome ->
                result <- outcome
                finished <- true

        return result
    }
```

Every caller of that function has to be told, out of band, that it takes a cancellation token, that it already
retries, and that a `TaskCanceledException` might mean a timeout rather than a shutdown. Forgetting to pass
`timeoutSource.Token` to an inner call is a silent bug.

In Axial the same three rules are three combinators wrapped around the workflow you already wrote:

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open Axial

type Connection = { Name: string }

let openConnection () = Task.FromResult { Name = "orders-db" }

let closeConnection (connection: Connection) (_: CancellationToken) =
    task { printfn $"closed {connection.Name}" } :> Task

let retryPayment =
    { RetryPolicy.noDelay 3 with
        Delay = fun attempt -> TimeSpan.FromMilliseconds(100.0 * float attempt)
        ShouldRetry =
            function
            | PaymentDeclined _ -> true
            | OrderNotFound _ -> false }

let checkoutOrder orderId : Flow<CheckoutEnv, CheckoutError, Receipt> =
    flow {
        let! _connection =
            Flow.acquireReleaseWith (Flow.fromTask (fun _ -> openConnection ())) closeConnection Flow.ok

        return! checkout orderId
    }
    |> Flow.Runtime.retry retryPayment
    |> Flow.Runtime.timeout (TimeSpan.FromSeconds 5.0) (PaymentDeclined "checkout timed out")
```

Running `checkoutOrder 42 |> Flow.run live` releases the connection before it returns the receipt:

```text
closed orders-db
```

Note what the type did not change to. `checkoutOrder` is still
`int -> Flow<CheckoutEnv, CheckoutError, Receipt>`, because cancellation, the connection's lifetime, and the retry
loop are the runtime's job rather than the caller's. The timeout produces a `CheckoutError` value that the caller
already handles instead of an exception the caller has to know about.

## Swap the boundary in a test

A test replaces the environment record and leaves the workflow alone:

```fsharp
let declineOnce =
    let mutable attempts = 0

    { FindTotal = fun _ -> Task.FromResult(Ok 19.99m)
      Charge =
        fun _ ->
            attempts <- attempts + 1

            if attempts = 1 then
                Task.FromResult(Error(PaymentDeclined "insufficient funds"))
            else
                Task.FromResult(Ok "ch_retry") }

let retried = checkoutOrder 42 |> Flow.run declineOnce
```

`retried` is `Exit.Success` with reference `ch_retry`, because the retry policy ran the second attempt. No container,
ambient service locator, or mocking framework is involved: the fake value has the same type as the live value.

The retry wraps the acquisition, so the second attempt opens and closes its own connection. Move the
`Flow.acquireReleaseWith` call outside the `Flow.Runtime.retry` call when every attempt should share one connection.

## What's next

1. [Why Flow?](why-flow.html) explains when this model earns its cost and when `Result` or `Task` is still the
   right answer.
2. [Installation and packages](installation.html) covers the package map.
3. [Add Axial to an existing Task application](existing-task-application.html) shows the one-module adoption path.
4. [Your first application](first-application.html) runs a Flow as an application root.
5. [Creating and running flows](../the-flow-type/index.html) covers the full construction and execution surface.
6. [Expected errors and defects](../error-handling/index.html) explains the error channel and defects.
7. [Dependencies, services, and layers](../dependencies/index.html) scales the environment record up.
