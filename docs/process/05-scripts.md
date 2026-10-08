---
title: Scripts
description: Author concise, safely interpolated process workflows.
---

Open `Axial.Process.DSL` for command-line-shaped authoring:

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
open Axial.Process.DSL

let connect (deviceId: string) (workspace: string) =
    cmd $"printf %%s {deviceId}"
    |> cwd workspace
    |> env "DEVICE_MODE" "service"
    |> timeout (TimeSpan.FromSeconds 30.0)
```

```fsharp run
connect "sensor 7; date" (IO.Path.GetTempPath())
|> Process.run
|> _.StdOut
|> shouldEqual "sensor 7; date"
```

Interpolation holes remain individual native arguments. Here `; date` is returned as text; it does not start another
command. No shell parses the value. Mark sensitive values explicitly:

```fsharp
let authenticate (token: string) =
    cmd $"printf %%s {secret token}"
```

```fsharp run
authenticate "s3cret" |> Process.render |> shouldEqual "printf \"%s\" ***"
authenticate "s3cret" |> Process.run |> _.StdOut |> shouldEqual "s3cret"
```

The real value reaches the process; renders, plans, and errors show `***`.

Use [`bash`](xref:M:Axial.Process.DSL.bash), [`sh`](xref:M:Axial.Process.DSL.sh), or [`pwsh`](xref:M:Axial.Process.DSL.pwsh) when shell syntax is required. Interpolated values are passed out of band as positional arguments:

```fsharp
let shout (value: string) =
    bash $"printf '%%s' {value} | tr '[:lower:]' '[:upper:]'"
```

```fsharp run
shout "it's $HOME" |> Process.run |> _.StdOut |> shouldEqual "IT'S $HOME"
```

`toFlow` and the stream constructors are lazy. Capture helpers configure retention without executing;
routing helpers choose destinations independently. `Process.run` starts a command, spec, or Flow
and returns its value. A direct command captures stdout for the caller:

```fsharp run
cmd $"printf hello" |> Process.run |> _.StdOut |> shouldEqual "hello"
```

`Process.run` raises `ProcessRunException` on failure. A successful command exposes its exit code:

```fsharp run
cmd $"true" |> Process.run |> _.ExitCode |> shouldEqual 0
```

For a longer script, compose commands inside `flow { }` and run the whole script once.
Each command becomes a lazy step with `toFlow`:

```fsharp
let script : Flow<ProcessHostEnvironment, ProcessError, string> =
    flow {
        let! first = cmd $"printf first" |> toFlow
        let! second = cmd $"printf second" |> toFlow
        return first.StdOut + second.StdOut
    }
```

```fsharp run
script |> Process.run |> shouldEqual "firstsecond"
```

If a script needs to inspect a process failure rather than raise, `Process.tryRun` returns `Exit`.
For example, an entry point can distinguish a failed stage from a startup error:

```fsharp run
match shText "exit 3" |> Process.tryRun with
| Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
    shouldEqual 3 failure.Stage.ExitCode
| other -> failwithf "Expected stage failure, got %A" other
```

The `With` variants accept an existing environment instead of supplying live services.

```fsharp
type AppEnvironment =
    { Process: IProcess
      Console: IConsole
      Region: string }
    interface IHasProcess with member this.Process = this.Process
    interface IHasConsole with member this.Console = this.Console

let reportingConsole = Console.test()
let appEnvironment =
    { Process = Process.live Clock.live FileSystem.live reportingConsole
      Console = reportingConsole
      Region = "eu" }
```

```fsharp run
cmd $"dotnet --version"
|> Process.runWith appEnvironment
|> _.ExitCode
|> shouldEqual 0
```

For application composition, use `Process.toFlow` and run the enclosing workflow with `Flow.run`.
`ProcessStream.runCollect` returns values plus completion; a stream must have a terminal before a host runner
can execute it. Effectful terminals may have `ProcessStreamError` errors, which `Process.tryRun` supports; use the ordinary
Flow runner when the application wants its own exit-code policy.
