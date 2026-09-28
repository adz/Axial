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
open Axial.Layers
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
        return answer.Trim().ToLowerInvariant() = "y"
    }
```

Because of the environment constraint, `confirm` cannot be called from a workflow that has not been given a
console, and it cannot reach the real terminal behind your back.

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

Line-oriented operations cover the common cases. Each returns a flow with an unconstrained error channel, so they
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
a finalizer, so an interrupted or failed workflow cannot leave the user with an invisible cursor or a green prompt:

```fsharp
let withHiddenCursor (console: IConsole) (body: Flow<AppEnv, Never, 'value>) : Flow<AppEnv, Never, 'value> =
    flow {
        do! Flow.scopeFinalizer(fun _ ->
            console.CursorVisible <- true
            Task.CompletedTask)

        do! Console.setCursorVisible false
        return! body
    }
    |> Flow.scoped
```

## Testing

Substitute any `IConsole` implementation. A recording console over `StringWriter` is usually enough, and it makes
assertions ordinary value comparisons:

`IConsole` is a wide interface, so implement it once in a test helper rather than in each test. This one reads its
input from a string, records what is written, and keeps terminal state in fields:

```fsharp
type RecordingConsole(input: string) =
    let reader = new StringReader(input)
    let output = new StringWriter()
    let error = new StringWriter()
    let mutable inputEncoding = Text.Encoding.UTF8
    let mutable outputEncoding = Text.Encoding.UTF8
    let mutable foreground = ConsoleColor.Gray
    let mutable background = ConsoleColor.Black
    let mutable cursorLeft = 0
    let mutable cursorTop = 0
    let mutable cursorVisible = true
    let mutable title = ""
    let mutable treatControlC = false

    member _.Written = output.ToString()
    member _.IsCursorVisible = cursorVisible

    interface IConsole with
        member _.In = reader
        member _.Out = output
        member _.Error = error
        member _.InputEncoding with get () = inputEncoding and set value = inputEncoding <- value
        member _.OutputEncoding with get () = outputEncoding and set value = outputEncoding <- value
        member _.IsInputRedirected = true
        member _.IsOutputRedirected = true
        member _.IsErrorRedirected = true
        member _.KeyAvailable = false
        member _.Read() = reader.Read()
        member _.ReadLine() = reader.ReadLine()
        member _.ReadKey _ = ConsoleKeyInfo()
        member _.Write value = output.Write value
        member _.WriteLine value = output.WriteLine value
        member _.WriteError value = error.Write value
        member _.WriteErrorLine value = error.WriteLine value
        member _.OpenStandardInput() = Stream.Null
        member _.OpenStandardOutput() = Stream.Null
        member _.OpenStandardError() = Stream.Null
        member _.Clear() = ()
        member _.Beep() = ()
        member _.ResetColor() = ()
        member _.ForegroundColor with get () = foreground and set value = foreground <- value
        member _.BackgroundColor with get () = background and set value = background <- value
        member _.CursorLeft with get () = cursorLeft and set value = cursorLeft <- value
        member _.CursorTop with get () = cursorTop and set value = cursorTop <- value
        member _.CursorVisible with get () = cursorVisible and set value = cursorVisible <- value
        member _.SetCursorPosition(left, top) = cursorLeft <- left; cursorTop <- top
        member _.Title with get () = title and set value = title <- value
        member _.TreatControlCAsInput with get () = treatControlC and set value = treatControlC <- value
```

With it, assertions are ordinary value comparisons:

```fsharp run
let answeredYes = RecordingConsole "y\n"
confirm "Continue?" |> Flow.run { Console = answeredYes } |> shouldEqual (Exit.Success true)
answeredYes.Written |> shouldEqual "Continue? [y/N] "

let answeredBlank = RecordingConsole "\n"
confirm "Continue?" |> Flow.run { Console = answeredBlank } |> shouldEqual (Exit.Success false)

let terminal = RecordingConsole ""
withHiddenCursor terminal (Console.writeLine "working") |> Flow.run { Console = terminal } |> shouldEqual (Exit.Success())
terminal.IsCursorVisible |> shouldEqual true
```

The cursor is visible again afterwards: the finalizer ran when the scope closed.

## Fable

`Console.live` is not compiled for Fable, and `Layer.succeed Console.live` fails with `PlatformNotSupportedException` there. A
workflow that must run on both .NET and Fable should depend on its own narrow output contract and adapt it to
`IConsole` only in the .NET host. See [packages and platforms](/notes/packages-and-platforms.html).

## Related

- [Service contracts](/dependencies/service-contracts.html): why the dependency is in the type.
- [Processes](/process/): the process service uses a console for stream wiring.
