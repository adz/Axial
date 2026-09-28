---
title: Output capture and destinations
description: Capture exact output, bound it, forward it, or write it elsewhere.
---

Specifications capture stdout and stderr by default. [`Process.toFlow`](xref:M:Axial.Process.Process.toFlow) preserves that policy and returns exact bytes and an encoding-aware text view:

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
// The examples on this page start real processes with the live service.
let host : ProcessHostEnvironment =
    { Process = Process.live Clock.live FileSystem.live Console.live
      Console = Console.live }

let stdoutOf (workflow: Flow<ProcessHostEnvironment, ProcessError, ProcessResult>) : string =
    match Flow.run host workflow with
    | Exit.Success result -> result.StdOut
    | Exit.Failure cause -> failwithf "%A" cause
```

```fsharp
let inspect : Flow<ProcessHostEnvironment, ProcessError, string * int> =
    flow {
        let! result =
            Process.command $"printf 'status: ok'"
            |> Process.toFlow

        let bytes = result.StdOutCapture.Bytes
        return result.StdOut, bytes.Length
    }
```

```fsharp run
inspect |> Flow.run host |> shouldEqual (Exit.Success("status: ok", 10))
```

Configure output with [`Process.stdout`](xref:M:Axial.Process.Process.stdout) and [`Process.stderr`](xref:M:Axial.Process.Process.stderr). Targets include complete capture, bounded tail capture, console forwarding, inherited handles, discard, files, sinks, callbacks, and tee composition.

```fsharp
let diagnose : Flow<ProcessHostEnvironment, ProcessError, ProcessResult> =
    Process.command $"printf 'diagnostics'"
    |> Process.stdout (OutputTarget.Tee [ OutputTarget.Console; OutputTarget.CaptureTail 65536 ])
    |> Process.toFlow
```

Use [`Process.stream`](xref:M:Axial.Process.Process.stream) when output must be handled before completion:

```fsharp
let events =
    Process.command $"printf 'one\ntwo\n'"
    |> Process.framing OutputFraming.Lines
    |> Process.stream

let outputLines : Flow<ProcessHostEnvironment, ProcessError, string list> =
    events
    |> FlowStream.choose (function
        | ProcessEvent.Output output -> Some output.Text
        | ProcessEvent.Completed _ -> None)
    |> FlowStream.runCollect
```

```fsharp run
outputLines |> Flow.run host |> shouldEqual (Exit.Success [ "one"; "two" ])
```

The stream emits `ProcessEvent.Output` values followed by one `ProcessEvent.Completed`. Each output event identifies its stage, channel, text, and timestamp. Pulling provides bounded backpressure. Ending consumption early interrupts the producer fiber, which terminates the native process topology before the Flow scope closes.

`OutputTarget.Inherit` gives the child the host handle directly. Inherited output cannot be observed, captured, or combined in a tee because it bypasses Axial's redirected streams.
