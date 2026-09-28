---
title: Commands and composition
description: Build immutable process specifications and connect native streams.
---

[`Process.command`](xref:M:Axial.Process.Process.command) safely tokenizes an interpolated command template and returns a runnable `ProcessSpec`. Each
interpolation hole becomes one native argument:

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
let status (repository: string) =
    Process.command $"git status --short"
    |> Process.workingDirectory repository
    |> Process.environment "CI" "true"
    |> Process.timeout (TimeSpan.FromSeconds 15.0)
```

Apply command-specific configuration before connecting stages. [`Process.arg`](xref:M:Axial.Process.Process.arg), [`Process.secretArg`](xref:M:Axial.Process.Process.secretArg), [`Process.workingDirectory`](xref:M:Axial.Process.Process.workingDirectory), [`Process.environment`](xref:M:Axial.Process.Process.environment), [`Process.removeEnvironment`](xref:M:Axial.Process.Process.removeEnvironment), [`Process.encoding`](xref:M:Axial.Process.Process.encoding), and [`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) require a one-command specification.

Connect stdout to the next stage with [`Process.pipe`](xref:M:Axial.Process.Process.pipe):

```fsharp
let countLines : Flow<ProcessHostEnvironment, ProcessError, ProcessResult> =
    Process.command $"printf 'error one\nerror two\n'"
    |> Process.pipe (Process.command $"wc -l")
    |> Process.toFlow
```

```fsharp run
stdoutOf countLines |> _.Trim() |> shouldEqual "2"
```

The DSL offers the same model with shorter names:

```fsharp
open Axial.Process.DSL

let shout (value: string) =
    cmd $"printf %%s {value}"
    => cmd $"tr '[:lower:]' '[:upper:]'"
    |> timeout (TimeSpan.FromSeconds 5.0)
    |> capture
```

The interpolated value is one argument, even with spaces or shell characters in it:

```fsharp run
stdoutOf (shout "hello; world") |> shouldEqual "HELLO; WORLD"
```

Each interpolation hole becomes one argument. Use [`secret`](xref:M:Axial.Process.DSL.secret) when plans, failures, and transcripts must show `***` instead of the real value. Use [`cmdText`](xref:M:Axial.Process.DSL.cmdText) only for fixed command text.

Use [`Process.commandArgs`](xref:M:Axial.Process.Process.commandArgs) when arguments already exist as a list. It has the same immutable
specification result, without command-line parsing.

[`Process.plan`](xref:M:Axial.Process.Process.plan) returns a redacted, serializable description without executing anything. [`Process.render`](xref:M:Axial.Process.Process.render) returns a redacted shell-like diagnostic string; it is not a shell command generator.
