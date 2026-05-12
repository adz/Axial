---
weight: 10
title: Ref (Atomic References)
description: Shared mutable state with atomic updates in FsFlow.
---

# Ref (Atomic References)

`Ref<'T>` is a handle to a mutable reference that can be updated atomically. It is the simplest way to manage shared state between multiple concurrent parts of your application.

> **Note:** `Ref` is currently available on **.NET** only.

## Why Use Ref?

In a functional program, state is usually passed as parameters or through the environment. However, some scenarios—like a shared counter, a cache, or a status flag—require multiple concurrent workflows to observe and update the same value. 

`Ref` provides a thread-safe way to do this without manual `lock` or `Monitor` calls.

## Creating a Ref

Use `Ref.make` to create a new reference with an initial value. Like all things in FsFlow, creating a `Ref` is an effectful operation that returns a `Flow`.

```fsharp
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
        printfn "Current value: %d" value
        
        do! Ref.set (value + 1) counter
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
        next, next // (new state, return value)
    ) counter
```

## Example: Shared Progress Tracker

```fsharp
let trackProgress (total: int) =
    flow {
        let! progress = Ref.make 0
        
        let doStep = 
            flow {
                let! current = Ref.modify (fun p -> p + 1, p + 1) progress
                printfn "Progress: %d/%d" current total
            }
            
        // Run several steps
        for _ in 1 .. total do
            do! doStep
            
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
| `modify` | `('T -> 'T * 'v) -> Ref<'T> -> Flow<'env, 'none, 'v>` | Atomically updates the value and returns a derived result. |
