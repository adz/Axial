---
title: Commands and composition
description: Build immutable process specifications and connect native streams.
---

[`Process.command`](xref:M:Axial.Process.Process.command) safely tokenizes an interpolated command template and returns a runnable `ProcessSpec`. Each
interpolation hole becomes one native argument:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let status =
    Process.command $"git status --short"
    |> Process.workingDirectory repository
    |> Process.environment "CI" "true"
    |> Process.timeout (TimeSpan.FromSeconds 15)
```

Apply command-specific configuration before connecting stages. [`Process.arg`](xref:M:Axial.Process.Process.arg), [`Process.secretArg`](xref:M:Axial.Process.Process.secretArg), [`Process.workingDirectory`](xref:M:Axial.Process.Process.workingDirectory), [`Process.environment`](xref:M:Axial.Process.Process.environment), [`Process.removeEnvironment`](xref:M:Axial.Process.Process.removeEnvironment), [`Process.encoding`](xref:M:Axial.Process.Process.encoding), and [`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) require a one-command specification.

Connect stdout to the next stage with [`Process.pipe`](xref:M:Axial.Process.Process.pipe):

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let countErrors =
    Process.command $"journalctl --priority=err"
    |> Process.pipe (Process.command $"wc -l")
    |> Process.toFlow
```

The DSL offers the same model with shorter names:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
open Axial.Process.DSL

let result =
    cmd $"printf %s {value}"
    => cmd $"tr '[:lower:]' '[:upper:]'"
    |> timeout (TimeSpan.FromSeconds 5)
    |> capture
```

Each interpolation hole becomes one argument. Use [`secret`](xref:M:Axial.Process.DSL.secret) when plans, failures, and transcripts must show `***` instead of the real value. Use [`cmdText`](xref:M:Axial.Process.DSL.cmdText) only for fixed command text.

Use [`Process.commandArgs`](xref:M:Axial.Process.Process.commandArgs) when arguments already exist as a list. It has the same immutable
specification result, without command-line parsing.

[`Process.plan`](xref:M:Axial.Process.Process.plan) returns a redacted, serializable description without executing anything. [`Process.render`](xref:M:Axial.Process.Process.render) returns a redacted shell-like diagnostic string; it is not a shell command generator.
