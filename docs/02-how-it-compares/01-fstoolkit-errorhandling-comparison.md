---
title: FsToolkit.ErrorHandling Comparison
description: When to use FsToolkit.ErrorHandling, when to use Flow, and how to combine them.
---

# FsToolkit.ErrorHandling Comparison

[FsToolkit.ErrorHandling](https://demystifyfp.gitbook.io/fstoolkit-errorhandling/) and Axial both make expected
failure explicit, but they operate at different levels.

FsToolkit.ErrorHandling provides computation expressions and combinators for types such as `Result`,
`Async<Result<_, _>>`, and `Task<Result<_, _>>`. It makes railway-oriented application code concise without adding a
runtime model.

Axial's `Flow<'env, 'error, 'value>` also has a typed error channel. In addition, it describes the environment a
workflow requires and gives execution a common model for interruption, resource lifetime, parallel composition,
retry, and defects.

## The difference in one table

| Concern | FsToolkit.ErrorHandling | Axial |
| --- | --- | --- |
| Expected failure | `Result` in the chosen carrier | The `'error` channel of `Flow` |
| Dependencies | Ordinary function parameters or closures | The `'env` parameter, read with `Flow.envWith` |
| Execution carrier | Chosen up front: `Result`, `AsyncResult`, `TaskResult`, and related builders | `Flow` describes the workflow; the runtime executes it |
| Cancellation | The underlying `Async` or `Task` code owns token propagation | The runtime passes its cancellation token to cold work and represents cancellation as `Cause.Interrupt` |
| Defects | Usually faulted tasks or raised exceptions outside `Result` | Bound work's exceptions become `Cause.Die`, so defects participate in Flow's concurrency semantics |
| Resource lifetime | Ordinary `use`, `use!`, `try/finally`, or application helpers | `use` and `use!` in `flow { }` for lexical lifetimes; `Flow.scoped` and the `Flow.scope...` operations for wider ownership |
| Retry, timeout, and parallel policy | Application code or another library | Runtime combinators over the workflow |

Neither approach replaces domain validation. Both can carry a validation error type; accumulating independent errors
still requires a validation abstraction rather than monadic short-circuiting.

## `taskResult` and environment-free Flow

The closest Flow equivalent to a `taskResult { }` expression is `Flow<'error, 'value>`, an abbreviation for
`Flow<unit, 'error, 'value>`. Both forms short-circuit on an expected error and require no environment. Here is the
same operation in each computation expression.

### FsToolkit.ErrorHandling

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

```fsharp no-check reason="Uses FsToolkit.ErrorHandling, which the documentation build does not reference"
let loadCustomer repository customerId : Task<Result<Customer * Address, CustomerError>> =
    taskResult {
        let! customer = repository.Find customerId
        let! address = repository.LoadAddress customer.AddressId
        return customer, address
    }
```

### Axial

```fsharp
type Customer = { Id: int; AddressId: int }
type Address = { Street: string }
type CustomerError = CustomerNotFound of int

type CustomerRepository =
    { Find: int -> Task<Result<Customer, CustomerError>>
      LoadAddress: int -> Task<Result<Address, CustomerError>> }

let loadCustomerFlow (repository: CustomerRepository) customerId : Flow<CustomerError, Customer * Address> =
    flow {
        let! customer = ColdTask(fun _ -> repository.Find customerId)
        let! address = ColdTask(fun _ -> repository.LoadAddress customer.AddressId)
        return customer, address
    }
```

```fsharp run
let repository =
    { Find = fun id -> Task.FromResult(if id = 1 then Ok { Id = 1; AddressId = 10 } else Error(CustomerNotFound id))
      LoadAddress = fun _ -> Task.FromResult(Ok { Street = "1 Main St" }) }

loadCustomerFlow repository 1 |> Flow.run () |> shouldEqual (Exit.Success({ Id = 1; AddressId = 10 }, { Street = "1 Main St" }))
loadCustomerFlow repository 2 |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail(CustomerNotFound 2)))
```

A `Task` starts when the method that returns it runs, so the Flow version wraps each call in `ColdTask`: the call then
happens when the Flow runs, and every run makes it again.

The similar source hides an execution difference. Calling `loadCustomer` starts and returns a `Task`; calling
`loadCustomerFlow` returns a cold workflow description that starts when a Flow runtime runs it. The runtime passes its
cancellation token through cold Flow and `ColdTask` operations, so timeout, race, and parallel-composition operators
can interrupt participating work. An already-started `Task`, including the result of calling a `taskResult` function,
cannot receive that token after it has started; expose cancellation-aware task code as a token-taking factory or
`ColdTask` when it must participate.

Flow also classifies outcomes for concurrency. An `Error` remains an expected `Cause.Fail`, cancellation becomes
`Cause.Interrupt`, and an exception thrown by bound work becomes `Cause.Die`. Operators such as `Flow.zipPar` can then
interrupt a sibling after either an expected failure or defect, and preserve concurrent failures in the resulting
`Cause`, rather than flattening every non-success outcome into the task exception channel.

FsToolkit.ErrorHandling fits synchronous `Result` pipelines and small functions that compose `Task<Result<_, _>>` calls
without starting concurrent work or owning resources. Once a pipeline needs cancellation, a timeout, parallel work, or
cleanup, the environment-free Flow form handles them by the same rules as the rest of the application.

## Prefer Flow for application orchestration

Use Flow when composition itself needs a contract: required services, managed resources, interruption, retry policy,
or a distinction between expected failures and defects. The `flow { }` computation expression supports `use` and
`use!` for resources owned by one lexical block. `Flow.scoped` and `Flow.scopeAcquireRelease` cover lifetimes that
need an explicit runtime ownership boundary or extend beyond that block.

```fsharp
type Receipt = { CustomerId: int; Reference: string }

type PaymentGateway =
    { Charge: Customer -> Async<Result<Receipt, CustomerError>> }

type CheckoutEnv =
    { Customers: CustomerRepository
      Payments: PaymentGateway }

let checkout customerId : Flow<CheckoutEnv, CustomerError, Receipt> =
    flow {
        let! customers = Flow.envWith _.Customers
        let! payments = Flow.envWith _.Payments
        let! customer = ColdTask(fun _ -> customers.Find customerId)
        let! receipt = payments.Charge customer
        return receipt
    }
```

Here the environment is part of the workflow type rather than a closure or a parameter repeated through each layer.
At the application boundary, `Flow.run` returns an `Exit`; expected errors, defects, and interruption remain distinct
instead of sharing the task's exception channel.

## Use them together

Adoption does not require rewriting FsToolkit.ErrorHandling functions. The Flow computation expression binds
`Result<'value, 'error>` and `Async<Result<'value, 'error>>` directly, and `Task<Result<'value, 'error>>` through
`ColdTask`. All of them continue on `Ok` and short-circuit the Flow on `Error`.

For example, an existing eligibility check can remain an `asyncResult` function:

```fsharp no-check reason="Uses FsToolkit.ErrorHandling, which the documentation build does not reference"
let verifyCustomer customer : Async<Result<unit, CustomerError>> =
    asyncResult {
        do! verifyEmail customer.Email
        do! verifyAccount customer.Id
    }
```

A Flow can then bind the `taskResult`-based `loadCustomer` function through `ColdTask` and bind this `asyncResult`
function directly in the same block:

```fsharp no-check reason="Uses FsToolkit.ErrorHandling, which the documentation build does not reference"
let prepareOrder customerId : Flow<OrderEnv, CustomerError, Order> =
    flow {
        let! repository = Flow.envWith _.Customers
        let! customer, address =
            ColdTask(fun _ ->
                loadCustomer repository customerId)

        do! verifyCustomer customer // Async<Result<_, _>>
        return createOrder customer address
    }
```

Because `flow { }` binds all of these (tasks through `ColdTask`), leaf functions can keep returning `Result`,
`Async<Result<_, _>>`, or `Task<Result<_, _>>`, and FsToolkit.ErrorHandling can keep building them. Flow takes over where operations are
composed: where dependencies, cancellation, timeouts, retries, and parallel work come in.
