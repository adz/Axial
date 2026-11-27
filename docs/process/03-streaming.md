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
open Axial
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
the process service when it runs; timers use the runtime clock. `runCollect` produces
`Flow<'env, ProcessError, string list * ProcessCompletion>`; `Process.run` returns
the values and completion directly. `Completed result` includes the final `ProcessResult`;
`StoppedEarly` reports successful prefix stopping after cleanup.

`map` and `filter` preserve completion. `streamStdout` and `streamStderr` emit decoded chunks with stage,
timestamp, `ObservedChannel`, and `OriginalChannel` metadata. `streamOutputs` includes all readable,
unconnected channels. A merged stderr value has observed channel stdout and original channel stderr.

## What counts as a line

Lines end where `StreamReader.ReadLine` ends them: at LF, CRLF or a bare CR, with the terminator left out of the
text. Progress output such as `git --progress` or `curl` redraws one line by ending it with a bare CR, so each redraw
arrives as its own line as soon as it is written. A long run of redraws never builds up into one line over the
`maxLineLength` limit.

With `framing OutputFraming.Lines`, each observation's `Ending` says which terminator it was: `Lf`, `CrLf`, `Cr`,
or `EndOfOutput` for final text without one. A display that shows progress replaces the line that follows a `Cr`
line instead of adding it below. Text plus ending rebuilds the output exactly. Chunk observations have `Ending = None`.

```fsharp
let progress =
    cmd $"printf 'Counting 1/2\rCounting 2/2\ndone\n'"
    |> framing OutputFraming.Lines
    |> streamOutputs
    |> ProcessStream.map (fun output -> output.Text, output.Ending)
    |> ProcessStream.runCollect
```

```fsharp run
let redraws, _ = progress |> Process.run
shouldEqual
    [ "Counting 1/2", Some LineEnding.Cr
      "Counting 2/2", Some LineEnding.Lf
      "done", Some LineEnding.Lf ]
    redraws
```

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
