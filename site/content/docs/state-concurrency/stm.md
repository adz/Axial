---
weight: 30
title: STM (Software Transactional Memory)
description: Composable atomic transactions across multiple variables in FsFlow.
type: docs
---


Software Transactional Memory (STM) is a concurrency primitive that lets you compose multiple
atomic operations into a single **transaction**.

While `Ref` is perfect for updating a single variable, `STM` is designed for scenarios where you
need to update **multiple** variables consistently. FsFlow ensures that the entire transaction is
executed atomically, and it now supports `retry` / `orElse` style coordination for transactions
that need to wait on state changes or fall back to alternate branches.

> **Note:** `STM` is currently available on **.NET** only.

## Core Concepts

- **`TRef<'T>`**: A transactional reference. Similar to `Ref<'T>`, but designed to be used inside an STM transaction.
- **`stm { ... }`**: A computation expression used to compose transactional operations.
- **`STM.retry`**: Aborts the current branch and waits for a committed state change before retrying.
- **`STM.orElse`**: Tries a fallback branch when the first branch retries.
- **`STM.atomically`**: The bridge that executes an `stm` block as a single atomic effect within a `flow`.

## Basic Usage

### Defining a Transaction

Transactions are defined using the `stm {}` builder. You can read, write, and update multiple `TRef` values within a single block.

```fsharp
let transferFunds (fromAcc: TRef<decimal>) (toAcc: TRef<decimal>) (amount: decimal) =
    stm {
        let! currentFrom = TRef.get fromAcc
        
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
        do! Log.info "Transfer complete!"
    }
```

## Composition

STM transactions are first-class values. You can compose multiple small transactions into a larger one using `stm {}` before ever calling `atomically`. This is the "Software" in STM—it allows for modular, composable concurrency.

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

Using manual `lock` calls is error-prone and often leads to deadlocks when different parts of your
code acquire multiple locks in inconsistent orders.

FsFlow's `STM` keeps commit atomicity inside the engine and adds `retry` / `orElse` coordination
so a transaction can wait for a relevant state change or fall back to another branch without
exposing lock management in user code.

## Implementation Details

It is important to note that FsFlow's current STM implementation is based on a **global synchronizing lock** 
rather than an optimistic or lock-free model.

- **Atomicity**: The entire `stm {}` block is executed while holding a global lock, ensuring no other transaction can interfere.
- **Blocking Retry**: When `STM.retry` is called, the calling thread is suspended using `Monitor.Wait` until another transaction successfully commits a change.
- **Performance**: Because of the global lock, transactions are mutually exclusive. This is suitable for coordinating low-frequency state changes but may become a bottleneck under high contention.

This design prioritizes correctness and simplicity for the initial release while providing the standard 
STM programming model found in languages like Haskell or ZIO.
## API Reference: Module `TRef`

| Function | Signature | Description |
| :--- | :--- | :--- |
| `make` | `'T -> STM<TRef<'T>>` | Creates a new transactional reference. |
| `get` | `TRef<'T> -> STM<'T>` | Reads the value of a `TRef` within a transaction. |
| `set` | `'T -> TRef<'T> -> STM<unit>` | Sets the value of a `TRef` within a transaction. |
| `update` | `('T -> 'T) -> TRef<'T> -> STM<unit>` | Atomically updates a `TRef` within a transaction. |

## API Reference: Module `STM`

| Function | Signature | Description |
| :--- | :--- | :--- |
| `retry` | `STM<'T>` | Requests that the current branch waits and reruns after a committed change. |
| `orElse` | `STM<'T> -> STM<'T> -> STM<'T>` | Falls back to a second branch when the first branch retries. |
| `atomically` | `STM<'T> -> Flow<'env, 'none, 'T>` | Executes a composed transaction as an atomic flow. |
