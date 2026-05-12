---
weight: 30
title: STM (Software Transactional Memory)
description: Composable atomic transactions across multiple variables in FsFlow.
---

# STM (Software Transactional Memory)

Software Transactional Memory (STM) is a powerful concurrency primitive that allows you to combine multiple atomic operations into a single **transaction**. 

While `Ref` is perfect for updating a single variable, `STM` is designed for scenarios where you need to update **multiple** variables consistently. FsFlow ensures that the entire transaction is executed atomically—either all changes are committed, or none are.

> **Note:** `STM` is currently available on **.NET** only.

## Core Concepts

- **`TRef<'T>`**: A transactional reference. Similar to `Ref<'T>`, but designed to be used inside an STM transaction.
- **`stm { ... }`**: A computation expression used to compose transactional operations.
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
        printfn "Transfer complete!"
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

## Why use STM instead of Lock?

Using `lock` manually is error-prone and can lead to deadlocks if different parts of your code acquire multiple locks in a different order. STM eliminates deadlocks by managing a consistent commit order internally and makes complex state transitions much easier to write and maintain.

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
| `atomically` | `STM<'T> -> Flow<'env, 'none, 'T>` | Executes a composed transaction as an atomic flow. |
