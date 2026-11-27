---
title: "dropped-work (AXG012): keep hold of started work"
linkTitle: dropped-work
description: Reports Task, ValueTask and Fiber values that are discarded with ignore or a wildcard.
---

# dropped-work (AXG012)

## The problem

`saveAsync () |> ignore` starts a save and throws away the only handle to it. Nobody waits for it, so the program may
exit first. If it fails, the exception goes nowhere.

```fsharp
open System.Threading
open System.Threading.Tasks

/// Reported: the save runs unobserved.
let saveAndForget (save: CancellationToken -> Task) (token: CancellationToken) =
    save token |> ignore

/// Fixed: the flow waits for the save and sees its failure.
let saveAndWait (save: CancellationToken -> Task) : Flow<unit, Never, unit> =
    Flow.fromUnitTask save
```

The same goes for a forked fiber: join it, or let its scope own it.

## What it reports

A `Task`, `ValueTask` or `Fiber` value discarded by `ignore`, by `let _ =`, or as a statement. It follows type
abbreviations and aliased task factories, and casts do not hide a task.

Not reported: `let!` and `do!`, values kept in a list or array, `Async.Start` (it returns unit), and a discarded
`Flow.fork` description. A flow is cold: until it runs, no fiber starts.

The rule sees local discards only. Keeping a task in a variable does not prove anyone waits for it.

It is an error, and no allowance or note exempts it. Test projects are exempt, unless `AxialGuardrailsInTests=all`.
