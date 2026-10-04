---
title: Streaming output
description: Consume process output while a command is still running.
---

# Streaming output

Use `Process.streamStdoutLines` for decoded stdout lines as they arrive. It returns a `ProcessStream`;
its terminal returns the consumed value together with process completion. Stderr stays separate unless you
explicitly apply `mergeStderr`.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let host = ProcessHostEnvironment.live
```

```fsharp
let lines =
    Process.command $"printf 'ready\nsample 1\n'"
    |> Process.streamStdoutLines

let reports = lines |> ProcessStream.map _.ToUpperInvariant() |> ProcessStream.runCollect
```

```fsharp run
match Flow.run host reports with
| Exit.Success (values, ProcessCompletion.Completed result) ->
    shouldEqual ["READY"; "SAMPLE 1"] values
    shouldEqual 0 result.ExitCode
| other -> failwithf "Expected completed output, got %A" other
```

`map` and `filter` preserve completion. `streamStdout` and `streamStderr` emit decoded chunks with stage,
timestamp, `ObservedChannel`, and `OriginalChannel` metadata. `streamOutputs` includes all readable,
unconnected channels. A merged stderr value has observed channel stdout and original channel stderr.

## Handle output with application effects

Use `runForEachFlow` for console, filesystem, or other application services. `runForEach` takes a synchronous
callback and is appropriate for ordinary in-memory processing. Effectful terminals distinguish `ProcessFailed`
from `ConsumerFailed` errors; defects and interruption retain their Axial cause.

```fsharp
let printLines =
    lines
    |> ProcessStream.runForEachFlow (Console.writeLine<ProcessHostEnvironment, string>)
```

```fsharp run
match Flow.run host printLines with
| Exit.Success (ProcessCompletion.Completed result) -> shouldEqual 0 result.ExitCode
| other -> failwithf "Expected printed output, got %A" other
```

## Stop after a prefix

`take` and `takeWhile` stop and await the owned native process before returning successful `StoppedEarly`.
`take 0` never launches it. Caller cancellation still interrupts the Flow.

```fsharp
let firstLine =
    Process.command $"sh -c 'echo ready; sleep 30'"
    |> Process.streamStdoutLines
    |> ProcessStream.take 1
    |> ProcessStream.runCollect
```

```fsharp run
Flow.run host firstLine |> shouldEqual (Exit.Success (["ready"], ProcessCompletion.StoppedEarly))
```

## Bound retained output

Streams default to a 64 KiB stdout tail and separate 64 KiB stderr tails. Backpressure bounds pending output;
`runCollect` still retains every value it collects, so consume unbounded output incrementally with `runForEachFlow`
or `runFoldFlow`. `captureAll` opts into full stdout retention. `captureNothing` removes stdout retention while
keeping observations and the separate stderr diagnostic tail.

```fsharp
let retainedTail =
    Process.command $"printf 'first\nlast\n'"
    |> Process.captureTail 5
    |> Process.streamStdoutLines
    |> ProcessStream.runDrain
```

```fsharp run
match Flow.run host retainedTail with
| Exit.Success (ProcessCompletion.Completed result) ->
    shouldEqual "last\n" result.StdOut
    shouldEqual true result.StdOutTruncated
| other -> failwithf "Expected bounded completion, got %A" other
```

Lines exclude LF and one preceding CR, preserve blank lines and bare CR, and emit final unterminated text.
`maxLineLength` defaults to 1,048,576 UTF-16 code units; oversized lines fail with `LineTooLong`.

Use `ProcessStream.toFlowStream` when an ordinary `FlowStream` API is needed and successful process completion
can be discarded. The converted stream retains typed process failures, backpressure, and scoped cleanup.
For completion-aware processing, keep transformations and terminals in `ProcessStream`. After conversion,
application callback deadlines belong to the enclosing Flow; use `Flow.timeout` to bound them.
`ProcessStream` terminals include callbacks in the process overall timeout.
