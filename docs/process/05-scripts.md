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
// The examples on this page start real processes with the live service.
let host : ProcessHostEnvironment =
    { Process = Process.live Clock.live FileSystem.live Console.live
      Console = Console.live
      Clock = Clock.live }

let stdoutOf (workflow: Flow<ProcessHostEnvironment, ProcessError, ProcessResult>) : string =
    match Flow.run host workflow with
    | Exit.Success result -> result.StdOut
    | Exit.Failure cause -> failwithf "%A" cause
```

```fsharp
open Axial.Process.DSL

let connect (deviceId: string) (workspace: string) =
    cmd $"printf %%s {deviceId}"
    |> cwd workspace
    |> env "DEVICE_MODE" "service"
    |> timeout (TimeSpan.FromSeconds 30.0)
    |> toFlow
```

```fsharp run
stdoutOf (connect "sensor 7; rm -rf /" (IO.Path.GetTempPath())) |> shouldEqual "sensor 7; rm -rf /"
```

Interpolation holes remain individual native arguments. They are not concatenated into shell source. Mark sensitive values explicitly:

```fsharp
let authenticate (token: string) =
    cmd $"printf %%s {secret token}"
```

```fsharp run
authenticate "s3cret" |> Process.render |> shouldEqual "printf \"%s\" ***"
stdoutOf (authenticate "s3cret" |> toFlow) |> shouldEqual "s3cret"
```

The real value reaches the process; renders, plans, and errors show `***`.

Use [`bash`](xref:M:Axial.Process.DSL.bash), [`sh`](xref:M:Axial.Process.DSL.sh), or [`pwsh`](xref:M:Axial.Process.DSL.pwsh) when shell syntax is required. Interpolated values are passed out of band as positional arguments:

```fsharp
let shout (value: string) =
    bash $"printf '%%s' {value} | tr '[:lower:]' '[:upper:]'"
    |> toFlow
```

```fsharp run
stdoutOf (shout "it's $HOME") |> shouldEqual "IT'S $HOME"
```

`toFlow` and the stream constructors are lazy. Capture helpers configure retention without executing;
routing helpers choose destinations independently. `run` starts a command, spec, or Flow and returns its full
`Exit`. A direct command uses batch capture defaults, so output is available to the caller.

```fsharp run
match cmd $"printf hello" |> run with
| Exit.Success result -> shouldEqual "hello" result.StdOut
| other -> failwithf "Expected captured output, got %A" other
```

Use `runExitCode` at a command-line entry point. It returns an integer; direct commands default to console
routing and no stdout retention. Explicit routes and capture policies override those defaults.

```fsharp run
cmd $"true" |> runExitCode |> shouldEqual 0
```

`runWith` accepts an existing application environment and preserves the full outcome; command inputs need
`IHasProcess`. `runExitCodeWith` additionally requires `IHasConsole` to report failures.

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
let exit =
    cmd $"dotnet --version"
    |> runExitCodeWith appEnvironment

exit |> shouldEqual 0
```

For application composition, use `Process.toFlow` and run the enclosing workflow with `Flow.run`.
`ProcessStream.runCollect` returns values plus completion; a stream must have a terminal before a host runner
can execute it. Effectful terminals may have `ProcessStreamError` errors, which `run` supports; use the ordinary
Flow runner when the application wants its own exit-code policy.
