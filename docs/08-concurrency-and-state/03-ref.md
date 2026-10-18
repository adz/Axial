---
title: Ref (Atomic References)
description: Shared mutable state with atomic updates in Axial.
---

# Ref (Atomic References)

`Ref<'T>` holds a mutable value that concurrent workflows can update atomically.

```fsharp
open Axial
open Axial.State
```

## Why Use Ref?

In a functional program, state is usually passed as parameters or through the environment. Some state, such as a shared counter, a cache, or a status flag, needs several concurrent workflows to observe and update the same value. 

`Ref` uses lightweight per-instance locking to make updates atomic without manual `lock` management.

## Creating a Ref

Use `Ref.make` to create a new reference with an initial value. Creating a `Ref` returns a `Flow`.

```fsharp transcript
> open Axial.State;;
> let counted () : Flow<int> =
-     flow {
-         let! counter = Ref.make 0
-         do! Ref.update ((+) 1) counter
-         return! Ref.get counter
-     };;
> Flow.run () (counted ());;
val it: Exit<int,Never> = Success 1

> Flow.run () (counted ());;
val it: Exit<int,Never> = Success 1
```

Each run makes a fresh `Ref`, so running `counted` twice gives 1 both times.

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

Use `Ref.update` or `Ref.modify` to update a value atomically when multiple fibers share it.

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
let incrementAndGetWithHelper (counter: Ref<int>) =
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

The full `Ref` signature list — `make`, `get`, `set`, `update`, `modify`, and the `getAndSet` / `getAndUpdate` /
`updateAndGet` family — is in the [Ref API reference](/api/Axial.State.RefModule.html).

The [refs torture test](torture-tests/refs.html) runs every update operation from eight fibers at once and checks that
no update is lost and no two see the same value.
