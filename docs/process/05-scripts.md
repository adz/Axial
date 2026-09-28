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
open Axial.Process.DSL

let connect (deviceId: string) (workspace: string) =
    cmd $"printf %%s {deviceId}"
    |> cwd workspace
    |> env "DEVICE_MODE" "service"
    |> timeout (TimeSpan.FromSeconds 30.0)
    |> capture
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
authenticate "s3cret" |> Process.render |> shouldEqual "printf %s ***"
stdoutOf (authenticate "s3cret" |> capture) |> shouldEqual "s3cret"
```

The real value reaches the process; renders, plans, and errors show `***`.

Use [`bash`](xref:M:Axial.Process.DSL.bash), [`sh`](xref:M:Axial.Process.DSL.sh), or [`pwsh`](xref:M:Axial.Process.DSL.pwsh) when shell syntax is required. Interpolated values are passed out of band as positional arguments:

```fsharp
let shout (value: string) =
    bash $"printf '%%s' {value} | tr '[:lower:]' '[:upper:]'"
    |> capture
```

```fsharp run
stdoutOf (shout "it's $HOME") |> shouldEqual "IT'S $HOME"
```

[`capture`](xref:M:Axial.Process.DSL.capture), [`console`](xref:M:Axial.Process.DSL.console), and [`stream`](xref:M:Axial.Process.DSL.stream) are lazy: they turn the specification into a `Flow` or `FlowStream`. Capture selects complete stdout and stderr capture. Console forwards both channels while retaining structured completion data. Stream yields `ProcessEvent` values and must be consumed by a Flow before a host can run it.

[`run`](xref:M:Axial.Process.DSL.run) is the host execution verb: it starts a process Flow with live services and returns a command-line exit code. This keeps it distinct from [`Flow.run`](xref:M:Axial.Flow.run), which runs an application Flow with its explicit environment.

```fsharp run
cmd $"dotnet --version"
|> console
|> run
|> shouldEqual 0
```

Use [`runWith`](xref:M:Axial.Process.DSL.runWith) when an application already owns its environment. The environment must provide `IConsole` for reporting a typed process failure; it can carry any additional application services. The Flow's own type states whether it also needs `IProcess`.

```fsharp
type AppEnvironment =
    { Process: IProcess
      Console: IConsole
      Region: string }
    interface IHasProcess with member this.Process = this.Process
    interface IHasConsole with member this.Console = this.Console

let appEnvironment =
    { Process = Process.live Clock.live FileSystem.live Console.live
      Console = Console.live
      Region = "eu" }
```

```fsharp run
let exit =
    cmd $"dotnet --version"
    |> console
    |> runWith appEnvironment

exit |> shouldEqual 0
```

For application composition, use [`Process.toFlow`](xref:M:Axial.Process.Process.toFlow) (or the DSL's [`capture`](xref:M:Axial.Process.DSL.capture) or [`console`](xref:M:Axial.Process.DSL.console)) and start the enclosing workflow with [`Flow.run`](xref:M:Axial.Flow.run).
