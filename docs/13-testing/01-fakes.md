---
title: Test fakes
linkTitle: Test fakes
description: Reusable test double implementations for the wide platform-service interfaces.
---

# Test fakes

The platform services expose wide interfaces. Implement the double for each once, in a test helper, rather than in
every test. This page holds a recording `IConsole`; the [HTTP testing guide](/http/testing.html) covers the
one-method `IHttp` fake and its scripted variant.

## Recording console

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

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

It reads its input from a string, records what is written, and keeps terminal state in fields:

```fsharp
type AppEnv =
    { Console: IConsole }

    interface IHasConsole with
        member this.Console = this.Console

let confirm question : Flow<AppEnv, Never, bool> =
    flow {
        do! Console.write $"{question} [y/N] "
        let! answer = Console.readLine
        return
            match answer with
            | null -> false
            | value -> value.Trim().ToLowerInvariant() = "y"
    }
```

```fsharp run
let answeredYes = RecordingConsole "y\n"
confirm "Continue?" |> Flow.run { Console = answeredYes } |> shouldEqual (Exit.Success true)
answeredYes.Written |> shouldEqual "Continue? [y/N] "

let answeredEof = RecordingConsole ""
confirm "Continue?" |> Flow.run { Console = answeredEof } |> shouldEqual (Exit.Success false)
```

Read the [console guide](/console/console.html) for the service's usage; this fake exercises it without a terminal.
