---
title: Console
linkTitle: Console
description: Standard streams, redirection state, and terminal control as an explicit service.
---

`Axial.Console` replaces `System.Console` with a service a workflow must declare. `IConsole` covers the three
standard streams, the redirection and encoding state around them, and interactive terminal control — cursor, colour,
title, and key reads.

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

The environment constraint is the whole point: `confirm` cannot be called from a workflow that has not been given a
console, and it cannot reach the real terminal behind your back.

## Supplying the service

Implement `IHasConsole` on the application environment and supply `Console.live` at the host edge:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
type AppEnv =
    { Console: IConsole }

    interface IHasConsole with
        member this.Console = this.Console

let! exit = confirm "Continue?" |> Flow.startTask { Console = Console.live }
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

These operations do not produce typed failures. A console write that throws — a closed pipe, for instance — is a
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

For interactive programs the service exposes the terminal surface directly: `clear`, `beep`, `foregroundColor` /
`setForegroundColor`, `backgroundColor` / `setBackgroundColor`, `resetColor`, `cursorPosition` /
`setCursorPosition`, `cursorVisible` / `setCursorVisible`, `title` / `setTitle`, and `keyAvailable` / `readKey`.

`Console.setTreatControlCAsInput true` delivers Ctrl+C to `readKey` instead of signalling the process, which is what
a full-screen terminal application wants.

Every one of these is mutable terminal state that outlives the workflow that set it. Restore what you change through
a finalizer, so an interrupted or failed workflow cannot leave the user with an invisible cursor or a green prompt:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let withHiddenCursor (console: IConsole) body =
    flow {
        do! Flow.addFinalizer(fun _ ->
            console.CursorVisible <- true
            Task.CompletedTask)

        do! Console.setCursorVisible false
        return! body
    }
```

## Testing

Substitute any `IConsole` implementation. A recording console over `StringWriter` is usually enough, and it makes
assertions ordinary value comparisons:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let recorded = StringWriter()

let testConsole =
    { new IConsole with
        member _.Out = recorded
        member _.WriteLine(value) = recorded.WriteLine value
        // remaining members raise or return defaults
        }

let! exit = report "ready" |> Flow.startTask { Console = testConsole }
test <@ recorded.ToString().Trim() = "ready" @>
```

`IConsole` is a wide interface, so implement it once in a test helper — an abstract base returning defaults, with the
few members a suite exercises overridden — rather than in each test.

## Fable

`Console.live` is not compiled for Fable, and `Layer.succeed Console.live` fails with `PlatformNotSupportedException` there. A
workflow that must run on both .NET and Fable should depend on its own narrow output contract and adapt it to
`IConsole` only in the .NET host. See [packages and platforms](/notes/packages-and-platforms.html).

## Related

- [Service contracts](/dependencies/service-contracts.html) — why the dependency is in the type.
- [Processes](processes/index.html) — the process service uses a console for stream wiring.
