---
title: STM (Software Transactional Memory)
description: Composable atomic transactions across multiple variables in Axial.
---

# STM (Software Transactional Memory)

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.Threading
open Axial
open Axial.PlatformService
open Axial.State

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

Software Transactional Memory (STM) combines operations into one atomic **transaction**.

`Ref` updates one variable atomically. `STM` updates **several** variables in one atomic transaction, and supports `retry` / `orElse` style coordination for transactions
that need to wait on state changes or fall back to alternate branches.

## Core Concepts

- **`TRef<'T>`**: A transactional reference. Similar to `Ref<'T>`, but designed to be used inside an STM transaction.
- **`stm { ... }`**: A computation expression used to compose transactional operations.
- **`STM.retry`**: Aborts the current branch and waits for a committed state change before retrying.
- **`STM.orElse`**: Tries a fallback branch when the first branch retries.
- **`STM.atomically`**: The bridge that executes an `stm` block as a single atomic effect within a `flow`.

## Basic Usage

### Defining a Transaction

Use `stm {}` to combine reads and writes into one transaction.

`let!` binds the value produced by an STM step to the name on its left. `do!` binds an STM step returning `unit`.
`return!` uses another STM value as the result of the transaction.

```fsharp
let withdraw (amount: decimal) (account: TRef<decimal>) : STM<decimal> =
    stm {
        let! balance = TRef.get account
        // Wait until the account can cover the withdrawal instead of driving the balance negative.
        if balance < amount then return! STM.retry
        do! TRef.set (balance - amount) account
        return! TRef.get account
    }
```

Here is the same transaction with the left- and right-hand types shown:

```fsharp
let withdrawAnnotated (amount: decimal) (account: TRef<decimal>) : STM<decimal> =
    stm {
        let! (balance: decimal) =
            (TRef.get account: STM<decimal>)

        if balance < amount then
            return! (STM.retry: STM<unit>)

        do! (TRef.set (balance - amount) account: STM<unit>)
        return! (TRef.get account: STM<decimal>)
    }
```

```fsharp
let transferFunds (fromAcc: TRef<decimal>) (toAcc: TRef<decimal>) (amount: decimal) =
    stm {
        let! currentFrom = TRef.get fromAcc
        // Retry later if the source balance cannot cover the transfer.
        if currentFrom < amount then return! STM.retry
        do! TRef.set (currentFrom - amount) fromAcc
        do! TRef.update (fun balance -> balance + amount) toAcc
    }
```

### Running the Transaction

To execute an `stm` block, you use `STM.atomically`. This turns the transaction into a standard `Flow`.

```fsharp
let processTransfer fromAcc toAcc amount =
    flow {
        // Run the transaction atomically
        do! STM.atomically (transferFunds fromAcc toAcc amount)
        return "Transfer complete"
    }
```

## Wait and fall back

`STM.retry` suspends the transaction without blocking the thread; the next commit wakes it and the transaction
re-runs from the start. `STM.orElse` runs a fallback branch when the first branch retries:

```fsharp
let withdrawWaiting (amount: decimal) (account: TRef<decimal>) : STM<decimal> =
    stm {
        let! balance = TRef.get account
        if balance < amount then return! STM.retry
        do! TRef.set (balance - amount) account
        return! TRef.get account
    }
```

```fsharp run
// A forked withdrawer suspends on an empty account; the deposit commit wakes it and it completes.
let waitForFunds =
    flow {
        let! account = STM.atomically (TRef.make 0m)
        let! withdrawer = STM.atomically (withdrawWaiting 10m account) |> Flow.fork
        do! STM.atomically (TRef.update (fun balance -> balance + 10m) account)
        return! Fiber.join withdrawer
    }

(waitForFunds |> Flow.run () : Exit<decimal, unit>) |> shouldEqual (Exit.Success 0m)
```

```fsharp run
// When the first branch retries, orElse runs the fallback instead of waiting.
let fallback : Flow<unit, Never, string> =
    flow {
        let! account = STM.atomically (TRef.make 0m)
        let! outcome =
            STM.atomically (
                STM.orElse
                    (stm {
                        let! balance = TRef.get account
                        if balance < 10m then return! STM.retry
                        return "withdrew" })
                    (stm { return "insufficient funds" }))
        return outcome
    }

fallback |> Flow.run () |> shouldEqual (Exit.Success "insufficient funds")
```

```fsharp run
// An interrupted retry withdraws its wake-up signal and the flow ends with Cause.Interrupt.
let interruptedSource = new CancellationTokenSource()
interruptedSource.Cancel()

let waiting : Flow<unit, unit, decimal> = STM.atomically (stm {
    let! account = TRef.make 0m
    let! balance = TRef.get account
    if balance < 10m then return! STM.retry
    return balance })

match waiting.RunSynchronously((), interruptedSource.Token) with
| Exit.Failure Cause.Interrupt -> ()
| other -> failwithf "Expected Cause.Interrupt, got %A" other
```

## Composition

STM transactions are first-class values. You can compose multiple small transactions into a larger one using `stm {}` before ever calling `atomically`. Transactions built in separate functions combine into one atomic transaction.

```fsharp
let deposit (acc: TRef<decimal>) (amount: decimal) =
    TRef.update (fun x -> x + amount) acc

let batchTransfer acc1 acc2 acc3 amount =
    stm {
        // Composite transaction
        do! transferFunds acc1 acc2 amount
        do! deposit acc3 (amount / 2.0m)
    }
    |> STM.atomically
```
## Why use STM instead of lock?

Manual `lock` calls can deadlock when code acquires multiple locks in different orders.

Axial's `STM` keeps commit atomicity inside the engine and adds `retry` / `orElse` coordination
so a transaction can wait for a relevant state change or fall back to another branch without
exposing lock management in user code.

## Implementation Details

Axial's current STM implementation runs each transaction under one **global lock** rather than an optimistic or
lock-free model.

- **Atomicity**: The entire `stm {}` block is executed while holding a global lock, ensuring no other transaction can interfere.
- **Suspending retry**: When `STM.retry` is called, the transaction suspends without blocking a thread until another transaction commits, then reruns from the start. Every commit wakes every waiting transaction.
- **Performance**: Because of the global lock, transactions are mutually exclusive. This is suitable for coordinating low-frequency state changes but may become a bottleneck under high contention.

The global lock keeps the initial implementation simple while supporting transactional references, composable transactions, `retry`, and `orElse`.
## API Reference

The `TRef` signatures (`make`, `get`, `set`, `update`) are in the
[TRef API reference](/api/Axial.State.TRefModule.html); the `STM` signatures (`retry`, `orElse`, `atomically`) are in
the [STM API reference](/api/Axial.State.STM.html).

The [STM torture test](torture-tests/stm.html) runs hundreds of interrupted transfers against an auditor and checks that
every audit sees a consistent total.
