---
title: Commands and composition
description: Build immutable process specifications and connect native streams.
weight: 10
type: docs
---

`Process.command` safely tokenizes an executable and its arguments and returns a runnable `ProcessSpec`:

```fsharp
let status =
    Process.command "git" [ "status"; "--short" ]
    |> Process.workingDirectory repository
    |> Process.environment "CI" "true"
    |> Process.timeout (TimeSpan.FromSeconds 15)
```

Apply command-specific configuration before connecting stages. `Process.arg`, `secretArg`, `workingDirectory`, `environment`, `removeEnvironment`, `encoding`, and `successCodes` require a one-command specification.

Connect stdout to the next stage with `Process.pipe`:

```fsharp
let countErrors =
    Process.command "journalctl" [ "--priority=err" ]
    |> Process.pipe (Process.command "wc" [ "-l" ])
    |> Process.run
```

The DSL offers the same model with shorter names:

```fsharp
open Axial.Flow.Process.DSL

let result =
    cmd $"printf %s {value}"
    => cmd $"tr '[:lower:]' '[:upper:]'"
    |> timeout (TimeSpan.FromSeconds 5)
    |> capture
```

Each interpolation hole becomes one argument. Use `secret value` when plans, failures, and transcripts must show `***` instead of the real value. Use `cmdText` only for fixed command text.

`Process.plan` returns a redacted, serializable description without executing anything. `Process.render` returns a redacted shell-like diagnostic string; it is not a shell command generator.
