---
title: Ref (Atomic References)
description: Shared mutable state with atomic updates in Axial.
---

# Ref (Atomic References)

`Ref<'T>` is a handle to a mutable reference that can be updated atomically. It is the simplest way to manage shared state between multiple concurrent parts of your application.

```fsharp
open Axial
open Axial.State
```

## Why Use Ref?

In a functional program, state is usually passed as parameters or through the environment. However, some scenarios—like a shared counter, a cache, or a status flag—require multiple concurrent workflows to observe and update the same value. 

`Ref` provides a high-level abstraction for thread-safe state management. It encapsulates the synchronization logic (currently implemented using lightweight per-instance locking), ensuring that you can perform complex updates without manual `lock` management or risking data races.

## Creating a Ref

Use `Ref.make` to create a new reference with an initial value. Like all things in Axial, creating a `Ref` is an effectful operation that returns a `Flow`.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let counterWorkflow =
    flow {
        let! counter = Ref.make 0
        return counter
    }
```

## Reading and Writing

Use `Ref.get` to read the current value and `Ref.set` to overwrite it.

```fsharp
let workWithRef (counter: Ref<int>) =
    flow {
        let! value = Ref.get counter
        do! Ref.set (value + 1) counter
        return value
    }
```

## Atomic Updates

For concurrent safety, you should use `Ref.update` or `Ref.modify`. These operations ensure that the update is atomic, preventing race conditions when multiple fibers are updating the same reference simultaneously.

### `Ref.update`
Updates the value using a transformation function.

```fsharp
let increment (counter: Ref<int>) =
    Ref.update (fun x -> x + 1) counter
```

### `Ref.modify`
Updates the value and returns a derived result (a common pattern for "get and increment").

```fsharp
let incrementAndGet (counter: Ref<int>) =
    Ref.modify (fun x -> 
        let next = x + 1
        next, next // (return value, new state)
    ) counter
```

### The atomic family

`Ref.getAndSet`, `Ref.getAndUpdate`, and `Ref.updateAndGet` cover the common "get the previous/updated value while
changing state" cases without writing a custom `modify` function:

```fsharp
let incrementAndGet (counter: Ref<int>) =
    Ref.updateAndGet (fun x -> x + 1) counter

let snapshotAndReset (counter: Ref<int>) =
    Ref.getAndSet 0 counter
```

## Example: Shared Progress Tracker

```fsharp
let trackProgress (total: int) =
    flow {
        let! progress = Ref.make 0
        
        let doStep = Ref.updateAndGet (fun p -> p + 1) progress
            
        // Run several steps
        for _ in 1 .. total do
            do! doStep |> Flow.ignore
            
        return "Complete"
    }
```

## API Reference

| Function | Signature | Description |
| :--- | :--- | :--- |
| `make` | `'T -> Flow<'env, 'none, Ref<'T>>` | Creates a new reference with an initial value. |
| `get` | `Ref<'T> -> Flow<'env, 'none, 'T>` | Reads the current value of the reference. |
| `set` | `'T -> Ref<'T> -> Flow<'env, 'none, unit>` | Sets the value of the reference to a new value. |
| `update` | `('T -> 'T) -> Ref<'T> -> Flow<'env, 'none, unit>` | Atomically updates the value using the supplied function. |
| `modify` | `('T -> 'v * 'T) -> Ref<'T> -> Flow<'env, 'none, 'v>` | Atomically updates the value and returns a derived result. |
| `getAndSet` | `'T -> Ref<'T> -> Flow<'env, 'none, 'T>` | Sets the value and returns the value it held before the update. |
| `getAndUpdate` | `('T -> 'T) -> Ref<'T> -> Flow<'env, 'none, 'T>` | Updates the value and returns the value it held before the update. |
| `updateAndGet` | `('T -> 'T) -> Ref<'T> -> Flow<'env, 'none, 'T>` | Updates the value and returns the value after the update. |
