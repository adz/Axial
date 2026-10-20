---
title: Console
linkTitle: Console
description: Standard streams, redirection state, and terminal control as an explicit service.
---

`Axial.Console` replaces `System.Console` with a service a workflow must declare. `IConsole` covers the three
standard streams, the redirection and encoding state around them, and interactive terminal control: cursor, colour,
title, and key reads.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open Axial
open Axial.Console
```

```fsharp
let confirm question : Flow<#IHasConsole, Never, bool> =
    flow {
        do! Console.write $"{question} [y/N] "
        let! answer = Console.readLine
        return
            match answer with
            | null -> false
            | value -> value.Trim().ToLowerInvariant() = "y"
    }
```

`Console.readLine` returns `null` at end of input, so `confirm` treats EOF the same as a blank line: a non-affirmative answer, and it never calls `Trim` on a null value. The environment constraint requires callers of `confirm` to supply a console; the workflow cannot access the terminal without one.

## Supplying the service

Implement `IHasConsole` on the application environment and supply `Console.live` at the host edge:

```fsharp
type AppEnv =
    { Console: IConsole }

    interface IHasConsole with
        member this.Console = this.Console

let askToContinue () : Task<Exit<bool, Never>> =
    confirm "Continue?" |> Flow.startTask { Console = Console.live }
```

For a runtime assembled with [layers](/layers/index.html), wrap it: `Layer.succeed Console.live`. See
[building a base runtime](/dependencies/providing-the-environment.html).

## Reading and writing

Line-oriented operations read and write standard streams. Each returns a flow with an unconstrained error channel, so they
compose into a workflow with any failure type:

```fsharp
Console.write "partial"          // stdout, no newline
Console.writeLine "done"         // stdout
Console.writeError "partial"     // stderr, no newline
Console.writeErrorLine "failed"  // stderr
Console.read                     // next character as an int, -1 at end of input
Console.readLine                 // next line
```

`Console.input`, `Console.output`, and `Console.error` return the underlying `TextReader` and `TextWriter` values when
you need to hand a stream to another API. `Console.openStandardInput`, `openStandardOutput`, and `openStandardError`
return raw `Stream` values for binary work.

These operations do not produce typed failures. A console write that throws, for instance on a closed pipe, is a
defect, not an expected error. Handle it as described in [defects](/error-handling/defects.html) if the workflow
should survive it.

## Redirection and encoding

Check redirection before using anything interactive. A program whose output is piped into another process has no
cursor to move:

```fsharp
let report line : Flow<#IHasConsole, Never, unit> =
    flow {
        let! redirected = Console.isOutputRedirected

        if redirected then
            return! Console.writeLine line
        else
            do! Console.setForegroundColor ConsoleColor.Green
            do! Console.writeLine line
            return! Console.resetColor
    }
```

`Console.isInputRedirected`, `isOutputRedirected`, and `isErrorRedirected` report the state of each stream.
`Console.inputEncoding` and `Console.outputEncoding` read the current encodings; `setInputEncoding` and
`setOutputEncoding` change them.

## Terminal control

For interactive programs the service exposes terminal control directly: `clear`, `beep`, `foregroundColor` /
`setForegroundColor`, `backgroundColor` / `setBackgroundColor`, `resetColor`, `cursorPosition` /
`setCursorPosition`, `cursorVisible` / `setCursorVisible`, `title` / `setTitle`, and `keyAvailable` / `readKey`.

`Console.setTreatControlCAsInput true` delivers Ctrl+C to `readKey` instead of signalling the process, which is what
a full-screen terminal application wants.

Every one of these is mutable terminal state that outlives the workflow that set it. Restore what you change through
a finalizer, so an interrupted or failed workflow cannot leave the user with an invisible cursor or a green prompt.
The release is a flow, so it uses the same `Console` operation from the environment:

```fsharp
let withHiddenCursor (body: Flow<AppEnv, Never, 'value>) : Flow<AppEnv, Never, 'value> =
    flow {
        do! Flow.scopeFinalizer (Console.setCursorVisible true)

        do! Console.setCursorVisible false
        return! body
    }
    |> Flow.scoped
```

## Testing

Substitute an `IConsole` implementation. The interface is wide, so implement the recording double once in a test
helper rather than in each test. The [test fakes recipe](/testing/fakes.html) provides a complete `RecordingConsole`
— one that reads its input from a string, records what is written, and keeps terminal state in fields — with checked
assertions against the recorded output and cursor visibility.

## Fable

`Console.live` is not compiled for Fable, and `Layer.succeed Console.live` fails with `PlatformNotSupportedException` there. A
workflow that must run on both .NET and Fable should depend on its own narrow output contract and adapt it to
`IConsole` only in the .NET host. See [packages and platforms](/notes/packages-and-platforms.html).

## Related

- [Service contracts](/dependencies/service-contracts.html): why the dependency is in the type.
- [Processes](/process/): the process service uses a console for stream wiring.
