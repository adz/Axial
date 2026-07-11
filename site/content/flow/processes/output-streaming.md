---
weight: 20
title: Output And Streaming
description: Capture, bound, tee, redirect, and stream process output with backpressure.
type: docs
---


This page shows complete process execution in full-capture, direct-console, target, callback, and FlowStream modes.

## Full Capture

`capture` waits for every stage, validates every success policy, and retains final stdout plus combined stderr:

```fsharp
open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL

let workflow : Flow<ScriptEnvironment, ProcessError, unit> =
    flow {
        let! result =
            Input.text "alpha\nbeta\n"
            => cmd $"grep beta"
            |> capture

        printf "%s" result.StdOut
        printfn "stages=%A duration=%O" result.ExitCodes result.Duration
    }

Script.run workflow
```

Example output:

```text
beta
stages=[0] duration=00:00:00.0123456
```

`StdOutCapture.Bytes` contains exact bytes. `StdOutCapture.Text` is the encoding-aware view. Use `captureResult` when
exit codes are data and should not become a typed stage failure.

## Files, Streams, Writers, And Callbacks

```fsharp
Input.file "source.txt" => cmd $"gzip" => Output.file "source.txt.gz"
```

```fsharp
use target = new System.IO.MemoryStream()
let! result = cmd $"git archive HEAD" => Output.stream target
```

```fsharp
let builder = System.Text.StringBuilder()
use writer = new System.IO.StringWriter(builder)
let! result = cmd $"dotnet test" => Output.textWriter System.Text.Encoding.UTF8 writer
```

`Output.callback` receives exact byte chunks and returns `Async<unit>`. The executor awaits it, so callbacks and streams
apply backpressure. `Output.file` truncates, `Output.appendFile` appends, and the named `writeTo` and `appendTo` terminal
operations provide searchable equivalents.

## Bound Memory And Tee

Full capture is inappropriate for unbounded tools. Keep a diagnostic tail while forwarding and persisting complete
output:

```fsharp
cmd $"dotnet test --nologo"
|> stdout (
    Output.tee [
        Output.console
        Output.file "test.log"
        Output.captureTail (64 * 1024)
    ])
|> stderr (
    Output.tee [
        Output.console
        Output.captureTail (64 * 1024)
    ])
|> toFlow
```

Capture limits are bytes. `Console` is managed forwarding and remains observable. `Output.inheritHandles` gives the
child the host handle directly, allowing terminal detection and interactive formatting, but cannot be captured, teed,
or observed on that channel.

## Stream Directly To The Console

The shortest live-console mode is:

```fsharp
cmd $"dotnet test --nologo" |> console
```

It forwards both channels and returns the structured `ProcessResult` when the process exits.

## Compose A FlowStream

`stream` returns a cold, bounded `FlowStream` of attributed stdout/stderr events followed by one completion transcript:

```fsharp
let printEvent = function
    | ProcessEvent.Output output ->
        flow { printf "[%A:%d] %s" output.Channel output.Stage output.Text }
    | ProcessEvent.Completed result ->
        flow { printfn "completed in %O" result.Duration }

let workflow : Flow<ScriptEnvironment, ProcessError, unit> =
    cmd $"dotnet test --nologo"
    |> Process.pipeline
    |> Process.framing OutputFraming.Lines
    |> stream
    |> FlowStream.filter (function ProcessEvent.Output output -> output.Text <> "" | _ -> true)
    |> FlowStream.runForEachFlow printEvent

Script.run workflow
```

The one-event rendezvous prevents the producer from outrunning the consumer. Stopping consumption closes the Flow scope,
cancels the process tree, and releases streams and files.

## Parallel Capture

```fsharp
projects
|> Seq.map (fun project -> cmd $"dotnet test {project}")
|> captureParallel 4
```

The limit bounds active commands. Result order matches input order; failure or cancellation interrupts sibling work.
