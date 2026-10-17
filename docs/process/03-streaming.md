---
title: Streaming output
description: Consume process output while a command is still running.
---

# Streaming output

Some commands produce useful output long before they finish — a build, a log tail, a long-running server.
`streamStdoutLines` yields decoded stdout lines as they arrive. It returns a `ProcessStream`, and its terminal
returns the consumed value together with process completion. Stderr stays separate unless you apply `mergeStderr`.

`ProcessStream<'env, 'value>` is the stream **type**; the `ProcessStream` **module** holds the functions that
operate on it: `ProcessStream.map`, `filter`, `take`, and the `run*` terminals. This mirrors the `Flow` type and
its `Flow` module. A stream terminal returns a Flow, which a script executes with `Process.run` or an
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

Lines exclude LF and one preceding CR, preserve blank lines and bare CR, and emit final unterminated text.
`maxLineLength` defaults to 1,048,576 UTF-16 code units; oversized lines fail with `LineTooLong`.

Use `ProcessStream.toFlowStream` when an ordinary `FlowStream` API is needed and successful process completion
can be discarded. The converted stream retains typed process failures, backpressure, and scoped cleanup.
For completion-aware processing, keep transformations and terminals in `ProcessStream`. After conversion,
application callback deadlines belong to the enclosing Flow; use `Flow.timeout` to bound them.
`ProcessStream` terminals include callbacks in the process overall timeout.

## Combine streams or feed a queue or hub

Convert with `ProcessStream.toFlowStream` when process output becomes one source in a larger stream.
`FlowStream.mergePar` interleaves sources as values arrive; `zip` pairs their values. The sources must have
compatible environment, error, and value types; use `FlowStream.mapError` to align different error types.

```fsharp run
let processLines =
    cmd $"printf 'process 1\nprocess 2\n'" |> streamStdoutLines |> ProcessStream.toFlowStream
let applicationLines = FlowStream.fromSeq ["application 1"; "application 2"]
let combined = [processLines; applicationLines] |> FlowStream.mergePar |> FlowStream.runCollect
combined |> Process.run |> List.sort
|> shouldEqual ["application 1"; "application 2"; "process 1"; "process 2"]
```

`FlowStream.runIntoQueue` offers each value to a queue; `runIntoHub` publishes it to the hub's current
subscribers. Both return a lazy Flow to bind in your application. A bounded queue or a hub subscription
using `BackPressure` slows the producer when full. Start the consumer concurrently for a bounded buffer;
hub subscriptions must exist before publishing. These terminals leave the queue or hub open so other
producers can keep using it.

```fsharp run
let queued = flow {
    let! queue = Queue.unbounded ()
    do! cmd $"printf 'first\nsecond\n'"
        |> streamStdoutLines |> ProcessStream.toFlowStream |> FlowStream.runIntoQueue queue
    return! Dequeue.takeAll queue
}
queued |> Process.run |> shouldEqual ["first"; "second"]

let published =
    flow {
        let! hub = Hub.make ()
        let! subscription = hub |> Hub.subscribe QueueStrategy.Unbounded
        do! cmd $"printf 'first\nsecond\n'"
            |> streamStdoutLines |> ProcessStream.toFlowStream |> FlowStream.runIntoHub hub
        return! Dequeue.takeAll subscription
    } |> Flow.scoped
published |> Process.run |> shouldEqual ["first"; "second"]
```

If you need the final exit details as well as the published values, keep the `ProcessStream` and send
values with `runForEachFlow`. The resulting Flow returns `ProcessCompletion` and keeps process and
consumer failures distinct. The same pattern works with `Hub.publish line hub |> Flow.ignore`.

```fsharp run
let queuedWithCompletion = flow {
    let! queue = Queue.unbounded ()
    let! completion =
        cmd $"printf 'first\nsecond\n'" |> streamStdoutLines
        |> ProcessStream.runForEachFlow (fun line ->
            Queue.offer line queue |> Flow.ignore : Flow<_, Never, unit>)
    let! values = Dequeue.takeAll queue
    return values, completion
}
let queuedValues, queuedCompletion = queuedWithCompletion |> Process.run
shouldEqual ["first"; "second"] queuedValues
match queuedCompletion with
| ProcessCompletion.Completed result -> shouldEqual 0 result.ExitCode
| ProcessCompletion.StoppedEarly -> failwith "Expected completed output"
```

## Transform and fold without collecting every item

Keep `ProcessStream.map` and `filter` on the process stream when completion matters. `runFold` reduces output
in memory; `runFoldFlow` lets each step use application services or fail with an application error.

```fsharp run
let numbers () = cmd $"printf '1\n2\n3\n4\n'" |> streamStdoutLines
let evenTotal =
    numbers () |> ProcessStream.map int |> ProcessStream.filter (fun value -> value % 2 = 0)
    |> ProcessStream.runFold (+) 0
let total, _ = evenTotal |> Process.run
shouldEqual 6 total

let checkedTotal =
    numbers ()
    |> ProcessStream.runFoldFlow
        (fun total text -> (Flow.ok (total + int text) : Flow<_, string, int>))
        0
let checkedSum, _ = checkedTotal |> Process.run
shouldEqual 10 checkedSum

let observed = ResizeArray<string>()
numbers () |> ProcessStream.runForEach observed.Add |> Process.run |> ignore
shouldEqual ["1"; "2"; "3"; "4"] (List.ofSeq observed)
```

A synchronous callback is for ordinary in-memory work. Use `runForEachFlow` or `runFoldFlow` for dependencies
such as `IConsole`; those effects remain explicit in the environment. Typed consumer errors become
`ProcessStreamError.ConsumerFailed`, while process failures become `ProcessFailed`.

```fsharp run
let rejected =
    cmd $"printf 'ready\n'" |> streamStdoutLines
    |> ProcessStream.runForEachFlow (fun _ -> Flow.fail "rejected record")
match rejected |> Process.tryRun with
| Exit.Failure(Cause.Fail(ProcessStreamError.ConsumerFailed message)) ->
    shouldEqual "rejected record" message
| other -> failwithf "Expected consumer failure, got %A" other

let prefix =
    cmd $"printf 'ready\nstop\nlater\n'" |> streamStdoutLines
    |> ProcessStream.takeWhile (fun line -> line <> "stop") |> ProcessStream.runCollect
prefix |> Process.run |> shouldEqual (["ready"], ProcessCompletion.StoppedEarly)
```

## Observe stderr and channel provenance

Use `streamStderrLines` for stderr alone. With `streamOutputs`, keep the metadata to distinguish stage and
original channel even after merging. Chunk boundaries depend on native reads; reconstruct text per channel
rather than assuming one event per write.

```fsharp run
let errors =
    sh $"printf data; printf 'warning\n' >&2" |> streamStderrLines |> ProcessStream.runCollect
let errorLines, errorCompletion = errors |> Process.run
shouldEqual ["warning"] errorLines
match errorCompletion with
| ProcessCompletion.Completed result -> shouldEqual "data" result.StdOut
| ProcessCompletion.StoppedEarly -> failwith "Expected completed output"

let outputs =
    sh $"printf data; printf warning >&2"
    |> mergeStderr
    |> streamOutputs
    |> ProcessStream.runCollect
let events, _ = outputs |> Process.run
let allStdout = events |> List.forall (fun item -> item.ObservedChannel = OutputChannel.StdOut)
shouldEqual true allStdout
let original channel =
    events
    |> List.filter (fun item -> item.OriginalChannel = channel)
    |> List.map _.Text
    |> String.concat ""
shouldEqual "data" (original OutputChannel.StdOut)
shouldEqual "warning" (original OutputChannel.StdErr)
```

## Limit lines and choose ordinary stream APIs explicitly

The limit counts decoded UTF-16 code units, excluding line delimiters. `maxLineLength` is independent of
byte-tail capture: limiting retained output does not make an oversized observed line acceptable.

```fsharp run
let oversized =
    cmd $"printf abcde" |> maxLineLength 4 |> streamStdoutLines |> ProcessStream.runDrain
match oversized |> Process.tryRun with
| Exit.Failure(Cause.Fail(ProcessError.LineTooLong _)) -> ()
| other -> failwithf "Expected line limit failure, got %A" other

let ordinary =
    cmd $"printf 'a\nb\n'" |> streamStdoutLines |> ProcessStream.toFlowStream
    |> FlowStream.map _.ToUpperInvariant() |> FlowStream.runCollect
ordinary |> Process.run |> shouldEqual ["A"; "B"]
```

`streamStdout` and `streamStderr` provide metadata-bearing decoded chunks when line framing is inappropriate.
`framing OutputFraming.Lines` selects line framing for metadata-bearing observations, while the explicit
`stream*Lines` constructors select it for you. `overallTimeout` covers completion-aware callbacks; `timeout`
on a command ends at that stage's native exit. Use `drainTimeout` for lingering redirected I/O after all stages exit.
