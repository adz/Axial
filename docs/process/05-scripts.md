---
title: Scripts
description: Author concise, safely interpolated process workflows.
---

Open `Axial.Process.DSL` for command-line-shaped authoring:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
open Axial.Process.DSL

let workflow =
    cmd $"device-tool connect {deviceId}"
    |> cwd workspace
    |> env "DEVICE_MODE" "service"
    |> timeout (TimeSpan.FromSeconds 30)
    |> capture
```

Interpolation holes remain individual native arguments. They are not concatenated into shell source. Mark sensitive values explicitly:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let workflow =
    cmd $"device-tool authenticate {secret token}"
    |> capture
```

Use [`bash`](xref:M:Axial.Process.DSL.bash), [`sh`](xref:M:Axial.Process.DSL.sh), or [`pwsh`](xref:M:Axial.Process.DSL.pwsh) when shell syntax is required. Interpolated values are passed out of band as positional arguments:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let workflow =
    bash $"printf '%s' {value} | tr '[:lower:]' '[:upper:]'"
    |> capture
```

[`capture`](xref:M:Axial.Process.DSL.capture), [`console`](xref:M:Axial.Process.DSL.console), and [`stream`](xref:M:Axial.Process.DSL.stream) are lazy: they turn the specification into a `Flow` or `FlowStream`. Capture selects complete stdout and stderr capture. Console forwards both channels while retaining structured completion data. Stream yields `ProcessEvent` values and must be consumed by a Flow before a host can run it.

[`run`](xref:M:Axial.Process.DSL.run) is the host execution verb: it starts a process Flow with live services and returns a command-line exit code. This keeps it distinct from [`Flow.run`](xref:M:Axial.Flow.run), which runs an application Flow with its explicit environment.

```fsharp no-check reason="Runs the locally installed dotnet command through live services"
cmd $"dotnet --version"
|> console
|> run
```

Use [`runWith`](xref:M:Axial.Process.DSL.runWith) when an application already owns its environment. The environment must provide `IConsole` for reporting a typed process failure; it can carry any additional application services. The Flow's own type states whether it also needs `IProcess`.

```fsharp no-check reason="AppEnvironment is defined by the application"
let exit =
    cmd $"dotnet --version"
    |> console
    |> runWith appEnvironment
```

For application composition, use [`Process.toFlow`](xref:M:Axial.Process.Process.toFlow) (or the DSL's [`capture`](xref:M:Axial.Process.DSL.capture) or [`console`](xref:M:Axial.Process.DSL.console)) and start the enclosing workflow with [`Flow.run`](xref:M:Axial.Flow.run).
