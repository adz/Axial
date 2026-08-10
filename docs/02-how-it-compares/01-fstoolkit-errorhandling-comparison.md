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
| Resource lifetime | Ordinary `use`, `use!`, `try/finally`, or application helpers | `use` and `use!` in `flow { }` for lexical lifetimes; `Flow.acquireReleaseWith` and scopes for wider ownership |
| Retry, timeout, and parallel policy | Application code or another library | Runtime combinators over the workflow |

Neither approach replaces domain validation. Both can carry a validation error type; accumulating independent errors
still requires a validation abstraction rather than monadic short-circuiting.

## `taskResult` and environment-free Flow

The closest Flow equivalent to a `taskResult { }` expression is `Flow<'error, 'value>`, an abbreviation for
`Flow<unit, 'error, 'value>`. Both forms short-circuit on an expected error and require no environment. Here is the
same operation in each computation expression.

### FsToolkit.ErrorHandling

```fsharp no-check reason="Application-specific repository and domain types are omitted"
let loadCustomer repository customerId : Task<Result<Customer * Address, CustomerError>> =
    taskResult {
        let! customer = repository.Find customerId
        let! address = repository.LoadAddress customer.AddressId
        return customer, address
    }
```

### Axial

```fsharp no-check reason="Application-specific repository and domain types are omitted"
let loadCustomerFlow repository customerId : Flow<CustomerError, Customer * Address> =
    flow {
        let! customer = repository.Find customerId
        let! address = repository.LoadAddress customer.AddressId
        return customer, address
    }
```

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

Prefer FsToolkit.ErrorHandling when `Task<Result<_, _>>` is the honest contract and no larger runtime model is needed.
It is often the smaller choice for leaf functions and request handlers. Prefer the environment-free Flow form when
that local pipeline also needs Flow's execution, resource, or composition semantics.

## Prefer Flow for application orchestration

Use Flow when composition itself needs a contract: required services, managed resources, interruption, retry policy,
or a distinction between expected failures and defects. The `flow { }` computation expression supports `use` and
`use!` for resources owned by one lexical block. `Flow.acquireReleaseWith` and scopes cover lifetimes that need an
explicit acquisition boundary or extend beyond that block.

```fsharp no-check reason="Application-specific services and domain types are omitted"
type CheckoutEnv =
    { Customers: CustomerRepository
      Payments: PaymentGateway }

let checkout customerId : Flow<CheckoutEnv, CheckoutError, Receipt> =
    flow {
        let! customers = Flow.envWith _.Customers
        let! payments = Flow.envWith _.Payments
        let! customer = customers.Find customerId
        let! receipt = payments.Charge customer
        return receipt
    }
```

Here the environment is part of the workflow type rather than a closure or a parameter repeated through each layer.
At the application boundary, `Flow.run` returns an `Exit`; expected errors, defects, and interruption remain distinct
instead of sharing the task's exception channel.

## Use them together

Adoption does not require rewriting FsToolkit.ErrorHandling functions. The Flow computation expression binds its
common carriers directly: `Result<'value, 'error>`, `Async<Result<'value, 'error>>`, and
`Task<Result<'value, 'error>>` all continue on `Ok` and short-circuit the Flow on `Error`.

For example, an existing eligibility check can remain an `asyncResult` function:

```fsharp no-check reason="Application-specific customer rules are omitted"
let verifyCustomer customer : Async<Result<unit, CustomerError>> =
    asyncResult {
        do! verifyEmail customer.Email
        do! verifyAccount customer.Id
    }
```

A Flow can then bind both the `taskResult`-based `loadCustomer` function from above and this `asyncResult` function in
the same block:

```fsharp no-check reason="Uses the application-specific FsToolkit.ErrorHandling functions from the preceding examples"
let prepareOrder customerId : Flow<OrderEnv, CustomerError, Order> =
    flow {
        let! repository = Flow.envWith _.Customers
        let! customer, address = loadCustomer repository customerId // Task<Result<_, _>>
        do! verifyCustomer customer                              // Async<Result<_, _>>
        return createOrder customer address
    }
```

This broader bind surface lets Flow serve as the orchestration boundary without forcing leaf functions onto one
asynchronous carrier. Keep FsToolkit.ErrorHandling where it improves local `Result`, `Async<Result<_, _>>`, or
`Task<Result<_, _>>` pipelines. Introduce Flow where the application needs explicit dependencies or runtime
composition policy. If a pipeline needs neither, the FsToolkit.ErrorHandling carrier remains the smaller choice.
