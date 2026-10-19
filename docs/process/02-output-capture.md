---
title: Output capture and destinations
description: Capture exact output, bound it, forward it, or write it elsewhere.
---

Process output goes nowhere it isn't sent. By default, `Process.toFlow` retains all stdout and a 64 KiB
diagnostic tail of stderr; streaming retains 64 KiB tails on both channels. Neither forwards anything to the
console — that takes an explicit route.

| Execution boundary | Default forwarding | Stdout retention | Unmerged stderr retention |
| --- | --- | --- | --- |
| `toFlow`, direct `Process.tryRun` / `Process.run` | None | All | 64 KiB tail |
| `stream*` constructors | None | 64 KiB tail | 64 KiB tail |

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
let inspect () : ProcessResult =
    cmd $"printf 'status: ok'" |> Process.run
```

```fsharp run
let result = inspect ()
shouldEqual "status: ok" result.StdOut
shouldEqual 10 result.StdOutCapture.Bytes.Length
```

`Process.run` returns `ProcessResult`. In that result, `StdOutCapture` and
`StdErrCapture` are `CapturedOutput` values: retained `Bytes`, decoded `Text`, `Truncated`, and `Available`.

## Choose retention independently of routing

Retention and routing are separate choices. `captureAll`, `captureTail bytes`, and `captureNothing` affect final
effective stdout only; `captureStderrAll`, `captureStderrTail bytes`, and `captureStderrNothing` affect readable
unmerged stderr. Explicit capture settings override the execution mode and topology defaults.

`stdoutToConsole`, `stdoutToFile`, `stdoutToAppendFile`, `stdoutToDiscard`, and `stdoutToInheritHandles`
configure routes. Stderr has matching shortcuts. `stdoutTo` and `stderrTo` accept arbitrary targets, including
callbacks, sinks, and `OutputTarget.Tee`. A file or console destination does not disable capture.

```fsharp
let diagnose =
    cmd $"printf diagnostics"
    |> stdoutToConsole
    |> captureTail 5
```

To check console routing without writing to the terminal, supply a test console for this execution:

```fsharp run
let consoleEnvironment = ProcessHostEnvironment.liveWithConsole (Console.test())
let diagnosed = diagnose |> Process.runWith consoleEnvironment
shouldEqual "stics" diagnosed.StdOut
shouldEqual true diagnosed.StdOutTruncated
```

When the exit status is all you need, `captureNothing` drops stdout retention. It does not drop the stderr
diagnostic tail; apply `captureStderrNothing` for that too. Full capture holds the whole stream in memory, so a
tail or `captureNothing` is safer when output can be large.

## Fail instead of truncating

`maxOutputBytes stdout stderr` sets independent total byte caps for each stage's original stdout and stderr.
Pass `Some limit` for a channel or `None` to leave it unlimited. The cap counts pipe bytes before framing,
forwarding, or capture, including intermediate stages. Exceeding it kills the process tree and returns
`ProcessError.OutputTooLarge` with the stage, original channel, limit, and command. Unlike `captureTail`,
this does not return a truncated success. Inherited handles cannot be counted.

```fsharp run
let capped = cmd $"printf hello" |> maxOutputBytes (Some 5L) None |> Process.run
shouldEqual "hello" capped.StdOut
```

`hello` is exactly five bytes, so it fits the cap. A sixth byte exceeds it and returns `OutputTooLarge` with the
stage, channel, and limit:

```fsharp run
let tooLarge = cmd $"printf 'hello!'" |> maxOutputBytes (Some 5L) None |> Process.tryRun

match tooLarge with
| Exit.Failure(Cause.Fail(ProcessError.OutputTooLarge failure)) ->
    shouldEqual 0 failure.Stage
    shouldEqual OutputChannel.StdOut failure.Channel
    shouldEqual 5L failure.Limit
| other -> failwithf "Expected OutputTooLarge, got %A" other
```

## Exact bytes and availability

Capture is byte-based, and the text view is derived from those bytes. `CapturedOutput.Bytes` holds the exact
retained bytes; `Text` decodes them with the command's encoding. A byte tail can split a multibyte character,
producing replacement characters in the text view. `Truncated` reports omitted bytes. `Available` reports whether
Axial could read the channel at all.

Inherited handles go directly to the child and cannot be observed, captured, or included in a tee.
Piped intermediate channels cannot be independently captured or observed. Final stdout capture includes merged
stderr only after explicit `mergeStderr`. Aggregate stderr is bounded separately; stage captures expose the
individual diagnostics.

Flow callbacks run under the process cancellation token; native stream adapters pass it to
reads, writes, and flushes. They must cooperate with cancellation. Typed callback failures become
`OutputFailed`; defects remain defects. Native process I/O failures use `IoFailed`.
Adapter streams and text writers remain caller-owned. `Output.textWriter`
creates a fresh decoder per execution. On `netstandard2.1`, TextWriter operations lack token overloads.
