---
title: "raise-in-flow (AXG003): return typed failures from workflows"
linkTitle: raise-in-flow
description: Reports raise, failwith and similar calls inside flow { }, which callers cannot handle as errors.
---

# raise-in-flow (AXG003)

## The problem

A `Flow<'env, 'error, 'value>` lists its expected failures in `'error`. The compiler makes callers deal with them.
An exception raised inside `flow { }` skips that: it becomes a defect, which the caller's `Flow.catch` does not see.

```fsharp
type OrderError = | OutOfStock of string

/// Reported: OutOfStock is an expected failure, but callers only see a defect.
let reserveOrThrow (sku: string) (available: int) : Flow<unit, OrderError, unit> =
    flow {
        if available = 0 then failwith $"Out of stock: {sku}"
        return ()
    }

/// Fixed: the failure is in the type, and callers can recover from it.
let reserve (sku: string) (available: int) : Flow<unit, OrderError, unit> =
    flow {
        if available = 0 then return! Flow.fail (OutOfStock sku)
    }
```

Use `Flow.fail` for an expected failure and `Flow.die` for a bug that nothing should recover from.

## What it reports

`raise`, `failwith`, `failwithf`, `invalidOp`, `invalidArg` and `reraise` inside `flow { }`, including inside nested
`if`, `match`, bindings and lambdas.

For a standalone `raise`, `failwith`, `failwithf` or `invalidOp`, it offers a `return! Flow.fail …` fix. A call inside
a larger expression needs a manual rewrite.

## When it is deliberate

When a `try/with` right there catches and translates the exception, add a reason:

```fsharp
let start (proc: System.Diagnostics.Process) =
    if not (proc.Start()) then raise (Exception "Process did not start") // axial-allow-raise-in-flow: caught and translated by caller
```
