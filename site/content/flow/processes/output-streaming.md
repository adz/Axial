---
title: Output and streaming
description: Capture exact output or consume process events with backpressure.
weight: 20
type: docs
---

Specifications capture stdout and stderr by default. `Process.run` returns exact bytes and an encoding-aware text view:

```fsharp
let! result =
    Process.command "device-tool" [ "inspect" ]
    |> Process.run

printfn "%s" result.StdOut
let bytes = result.StdOutCapture.Bytes
```

Configure output with `Process.stdout` and `Process.stderr`. Targets include complete capture, bounded tail capture, console forwarding, inherited handles, discard, files, sinks, callbacks, and tee composition.

```fsharp
let! result =
    Process.command "device-tool" [ "diagnose" ]
    |> Process.stdout (OutputTarget.Tee [ OutputTarget.Console; OutputTarget.CaptureTail 65536 ])
    |> Process.run
```

Use `Process.stream` when output must be handled before completion:

```fsharp
let events =
    Process.command "device-tool" [ "watch" ]
    |> Process.framing OutputFraming.Lines
    |> Process.stream

let! collected = events |> FlowStream.runCollect
```

The stream emits `ProcessEvent.Output` values followed by one `ProcessEvent.Completed`. Each output event identifies its stage, channel, text, and timestamp. Pulling provides bounded backpressure. Ending consumption early interrupts the producer fiber, which terminates the native process topology before the Flow scope closes.

`OutputTarget.Inherit` gives the child the host handle directly. Inherited output cannot be observed, captured, or combined in a tee because it bypasses Axial's redirected streams.
