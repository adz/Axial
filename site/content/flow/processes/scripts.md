---
title: Scripts
description: Author concise, safely interpolated process workflows.
weight: 40
type: docs
---

Open `Axial.Flow.Process.DSL` for command-line-shaped authoring:

```fsharp
open Axial.Flow.Process.DSL

let workflow =
    cmd $"device-tool connect {deviceId}"
    |> cwd workspace
    |> env "DEVICE_MODE" "service"
    |> timeout (TimeSpan.FromSeconds 30)
    |> capture
```

Interpolation holes remain individual native arguments. They are not concatenated into shell source. Mark sensitive values explicitly:

```fsharp
let workflow =
    cmd $"device-tool authenticate {secret token}"
    |> capture
```

Use `bash`, `sh`, or `pwsh` when shell syntax is required. Interpolated values are passed out of band as positional arguments:

```fsharp
let workflow =
    bash $"printf '%s' {value} | tr '[:lower:]' '[:upper:]'"
    |> capture
```

The DSL's execution verbs are `run`, `capture`, `console`, and `stream`. `capture` selects complete stdout and stderr capture before running. `console` forwards both channels while retaining structured completion data. `stream` yields `ProcessEvent` values.

At a command-line host boundary, `Script.run console workflow` executes against live services, prints a typed process failure through the supplied console service, and returns a host exit code.
