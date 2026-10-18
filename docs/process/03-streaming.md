---
title: Streaming output
description: Consume process output while a command is still running.
---

# Streaming output

`streamStdoutLines` yields decoded stdout lines as they arrive. It returns a `ProcessStream`, and its terminal
returns the consumed value together with process completion. Stderr stays separate unless you apply `mergeStderr`.

`ProcessStream<'env, 'value>` is the stream **type**; the `ProcessStream` **module** provides `ProcessStream.map`, `filter`, `take`, and the `run*` terminals. A stream terminal returns a Flow, which a script executes with `Process.run` or an
application binds with `let!` inside the enclosing Flow. A `ProcessStream` cannot be passed to `Process.run` directly.

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
open Axial.Process.DSL

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let lines =
    cmd $"printf 'ready\nsample 1\n'"
    |> streamStdoutLines

let reports = lines |> ProcessStream.map _.ToUpperInvariant() |> ProcessStream.runCollect
```

```fsharp run
let values, completion = reports |> Process.run
shouldEqual ["READY"; "SAMPLE 1"] values
match completion with
| ProcessCompletion.Completed result -> shouldEqual 0 result.ExitCode
| ProcessCompletion.StoppedEarly -> failwith "Expected completed output"
```

`lines` is a `ProcessStream<'env, string>`: the value parameter is one decoded line, and `'env` supplies
process and clock services when it runs. `runCollect` produces
`Flow<'env, ProcessError, string list * ProcessCompletion>`; `Process.run` returns
the values and completion directly. `Completed result` includes the final `ProcessResult`;
`StoppedEarly` reports successful prefix stopping after cleanup.

`map` and `filter` preserve completion. `streamStdout` and `streamStderr` emit decoded chunks with stage,
timestamp, `ObservedChannel`, and `OriginalChannel` metadata. `streamOutputs` includes all readable,
unconnected channels. A merged stderr value has observed channel stdout and original channel stderr.

## Handle output with application effects

Use `runForEachFlow` when a line needs a console, filesystem, or other application service. `runForEach` takes a
synchronous callback and suits ordinary in-memory work. Effectful terminals keep `ProcessFailed` and
`ConsumerFailed` distinct; defects and interruption retain their Axial cause. Their Flow error type is
`ProcessStreamError<'consumerError>`; use `Process.tryRun` to inspect one inside `Exit`.

```fsharp
let printLines =
    lines
    |> ProcessStream.runForEachFlow (Console.writeLine<_, string>)
```

```fsharp run
match printLines |> Process.run with
| ProcessCompletion.Completed result -> shouldEqual 0 result.ExitCode
| ProcessCompletion.StoppedEarly -> failwith "Expected completed output"
```

## Stop after a prefix

`take` and `takeWhile` stop and wait for the owned native process to clean up before returning a successful
`StoppedEarly`. `take 0` never launches it. Caller cancellation still interrupts the Flow.

```fsharp
let firstLine =
    cmd $"sh -c 'echo ready; sleep 30'"
    |> streamStdoutLines
    |> ProcessStream.take 1
    |> ProcessStream.runCollect
```

```fsharp run
firstLine
|> Process.run
|> shouldEqual (["ready"], ProcessCompletion.StoppedEarly)
```

## Bound retained output

Streams keep a 64 KiB stdout tail and separate 64 KiB stderr tails by default. Backpressure bounds pending
output, but `runCollect` still retains every value it collects, so consume unbounded output incrementally with
`runForEachFlow` or `runFoldFlow`. `captureAll` opts into full stdout retention; `captureNothing` removes stdout
retention while keeping observations and the separate stderr diagnostic tail.

```fsharp
let retainedTail =
    cmd $"printf 'first\nlast\n'"
    |> captureTail 5
    |> streamStdoutLines
    |> ProcessStream.runDrain
```

```fsharp run
match retainedTail |> Process.run with
| ProcessCompletion.Completed result ->
    shouldEqual "last\n" result.StdOut
    shouldEqual true result.StdOutTruncated
| ProcessCompletion.StoppedEarly -> failwith "Expected completed output"
```

Use `ProcessStream.toFlowStream` when you need an ordinary `FlowStream` API; merging process output with other
sources, feeding queues and hubs, folding, stderr provenance, line limits, and conversion are covered in the
[FlowStream integration](flowstream-integration.html) recipe.
