---
weight: 20
title: Execution and Outcomes
description: How to run flows, handle their results, and combine them.
---

# Execution and Outcomes

Once you have defined a `Flow`, you must execute it to perform its effects and obtain a result. This page covers the mechanics of running flows, handling the different types of outcomes, and combining multiple flows.

## 1. Running a Flow

The primary entry point for execution is `Flow.run`. It requires an environment (even if just `()`) and returns a platform-specific deferred handle called an `Effect`.

```fsharp
let myFlow = Flow.succeed "Hello"

// On .NET: returns ValueTask<Exit<string, 'error>>
let! exit = Flow.run () myFlow

// On Fable: returns Async<Exit<string, 'error>>
let! exit = Flow.run () myFlow
```

### Unwrapping to Result

While `Exit` is the native outcome of a flow, you may want to bridge back to a standard F# `Result`. The pattern differs by platform because of the underlying threading model.

#### On .NET (Synchronous)
If you are at a synchronous boundary (like a console app `main` or a test), you can use the `Flow.toResult` helper. This blocks the current thread until the flow completes.

```fsharp
// returns Result<'value, 'error>
let res = Flow.toResult environment myFlow
```

#### On Fable (Asynchronous)
Because Fable targets (JavaScript, Erlang) are non-blocking, you must explicitly await the result within an `async` block.

```fsharp
let getResult () = async {
    let! exit = Flow.run environment myFlow
    return Exit.toResult exit // returns Result<'value, 'error>
}
```

> **Warning**: Both `Flow.toResult` and `Exit.toResult` will re-raise exceptions if the flow crashed (`Cause.Die`) or was interrupted. Only use these when you want to treat non-domain failures as fatal.

## 2. Understanding the Exit Outcome

The result of running a flow is always an `Exit<'value, 'error>`. This type represents the final state of the workflow.

```fsharp
match exitValue with
| Exit.Success value -> 
    printfn "The workflow succeeded with: %A" value

| Exit.Failure cause -> 
    // The workflow failed (see below)
    handleFailure cause
```

### The Cause of Failure

FsFlow distinguishes between expected domain failures and unexpected technical defects using the `Cause<'error>` type:

| Cause | Description | Source |
| :--- | :--- | :--- |
| `Fail 'error` | An **expected** failure in your domain logic. | `Flow.fail`, `Error` results. |
| `Die exn` | An **unexpected** defect or "panic". | Uncaught exceptions, `Flow.die`. |
| `Interrupt` | The workflow was **cancelled** from the outside. | `CancellationToken`. |

## 3. Unwrapping and Transforming

While pattern matching is the recommended way to handle outcomes, several helpers exist for quick transformations.

### Exit.toResult

Converts an `Exit` into a standard F# `Result<'value, 'error>`. 

*   `Success v` -> `Ok v`
*   `Failure (Fail e)` -> `Error e`
*   `Failure (Die ex)` -> **re-raises `ex`**
*   `Failure Interrupt` -> **raises `OperationCanceledException`**

This is useful when you want to bridge back to libraries that only understand `Result`, but it assumes that defects should be treated as fatal exceptions.

### Mapping Values

You can transform the content of an `Exit` without unwrapping it:

*   `Exit.map`: Transform the success value.
*   `Exit.mapError`: Transform the domain error.
*   `Exit.mapBoth`: Transform both at once.

## 4. Combining Multiple Flows

FsFlow provides powerful combinators for orchestrating multiple workflows.

### Sequential Pairing (Zip)

`Flow.zip` runs two flows in sequence. The second flow only runs if the first one succeeds. The result is a tuple of both values.

```fsharp
let combined = Flow.zip flowA flowB
// combined: Flow<'env, 'error, 'a * 'b>
```

### Parallel Pairing (ZipPar)

`Flow.zipPar` runs two flows in parallel. If either flow fails, the other is **immediately interrupted** to save resources.

```fsharp
let parallel = Flow.zipPar flowA flowB
```

### Working with Sequences

When you have a collection of items to process:

*   **`Flow.traverse`**: Maps each item to a flow and runs them sequentially, stopping at the first failure.
*   **`Flow.sequence`**: Takes a list of flows and runs them sequentially.

```fsharp
let processAll = items |> Flow.traverse (fun item -> processItem item)
```

### Competitive Execution (Race)

`Flow.race` runs two flows in parallel and returns the result of the **first one to finish** (success or failure). The "loser" is interrupted immediately.

```fsharp
let fastestResult = Flow.race longTask timeoutTask
```

> **Note**: `Flow.race` is currently only supported on .NET.
