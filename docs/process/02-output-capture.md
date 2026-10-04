---
title: Output capture and destinations
description: Capture exact output, bound it, forward it, or write it elsewhere.
---

`Process.toFlow` captures all stdout and a separate 64 KiB stderr diagnostic tail by default. Streaming defaults
to bounded 64 KiB tails on both channels. Neither mode forwards output to the console unless you configure a route.

| Execution boundary | Default forwarding | Stdout retention | Unmerged stderr retention |
| --- | --- | --- | --- |
| `toFlow`, direct `run` / `runWith` | None | All | 64 KiB tail |
| `stream*` constructors | None | 64 KiB tail | 64 KiB tail |
| Direct `runExitCode` / `runExitCodeWith` | Console | Nothing | 64 KiB tail |

Explicit settings override these defaults. Runners applied to an existing Flow preserve its configured policies.

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
let host = ProcessHostEnvironment.liveWithConsole (Console.test())
```

```fsharp
let inspect =
    flow {
        let! result = cmd $"printf 'status: ok'" |> toFlow
        return result.StdOut, result.StdOutCapture.Bytes.Length
    }
```

```fsharp run
inspect |> Flow.run host |> shouldEqual (Exit.Success("status: ok", 10))
```

## Choose retention independently of routing

`captureAll`, `captureTail bytes`, and `captureNothing` affect final effective stdout only.
`captureStderrAll`, `captureStderrTail bytes`, and `captureStderrNothing` affect readable unmerged stderr.
Explicit capture settings override the execution mode and topology defaults.

`stdoutToConsole`, `stdoutToFile`, `stdoutToAppendFile`, `stdoutToDiscard`, and `stdoutToInheritHandles`
configure routes. Stderr has matching shortcuts. `stdoutTo` and `stderrTo` accept arbitrary targets, including
callbacks, sinks, and `OutputTarget.Tee`. A file or console destination does not disable capture.

```fsharp
let diagnose =
    cmd $"printf diagnostics"
    |> stdoutToConsole
    |> captureTail 5
    |> toFlow
```

```fsharp run
match Flow.run host diagnose with
| Exit.Success result ->
    shouldEqual "stics" result.StdOut
    shouldEqual true result.StdOutTruncated
| other -> failwithf "Expected captured console output, got %A" other
```

Use `captureNothing` when routed or consumed output and the exit status suffice. It does not remove the stderr
diagnostic tail; apply `captureStderrNothing` to opt out of that too. Full capture can use unbounded memory.

## Exact bytes and availability

`CapturedOutput.Bytes` contains exact retained bytes; `Text` decodes them with the command's encoding.
A byte tail can split a multibyte character, producing replacement characters in the text view.
`Truncated` reports omitted bytes. `Available` reports whether Axial could read the channel at all.

Inherited handles go directly to the child and cannot be observed, captured, or included in a tee.
Piped intermediate channels cannot be independently captured or observed. Final stdout capture includes merged
stderr only after explicit `mergeStderr`. Aggregate stderr is bounded separately; stage captures expose the
individual diagnostics.

Callbacks receive the execution token through `Async.CancellationToken`; native stream adapters pass it to
reads, writes, and flushes. They must cooperate with cancellation. Callback exceptions are defects; actual
process I/O failures use `IoFailed`. Adapter streams and text writers remain caller-owned. `Output.textWriter`
creates a fresh decoder per execution. On `netstandard2.1`, TextWriter operations lack token overloads.
