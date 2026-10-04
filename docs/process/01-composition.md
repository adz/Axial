---
title: Commands and composition
description: Build immutable process specifications and connect native streams.
---

[`cmd`](xref:M:Axial.Process.DSL.cmd) safely tokenizes an interpolated command template and returns an immutable `ProcessCommand`. Each
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
open Axial.Process.DSL

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual

let stdoutOf (outcome: Exit<ProcessResult, ProcessError>) =
    match outcome with
    | Exit.Success result -> result.StdOut
    | Exit.Failure cause -> failwithf "%A" cause
```

Start with one command; `run` supplies the execution services:

```fsharp run
match cmd $"printf hello" |> run with
| Exit.Success result -> shouldEqual "hello" result.StdOut
| other -> failwithf "Expected greeting, got %A" other
```

Then add settings to the command before running or connecting it:

```fsharp
let status (repository: string) : ProcessCommand =
    cmd $"git status --short"
    |> cwd repository
    |> env "CI" "true"
    |> timeout (TimeSpan.FromSeconds 15.0)
```

Apply `cwd`, `env`, `timeout`, and per-command routes before connecting stages. Qualified helpers such as
[`Process.arg`](xref:M:Axial.Process.Process.arg), [`Process.secretArg`](xref:M:Axial.Process.Process.secretArg),
[`Process.removeEnvironment`](xref:M:Axial.Process.Process.removeEnvironment),
[`Process.encoding`](xref:M:Axial.Process.Process.encoding), and
[`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) also take a `ProcessCommand`.

Connect stdout to the next stage with `=>` (or the pipeline helper `pipeTo`). Two `ProcessCommand` values
become a `ProcessSpec`; command setters then require configuring a command before joining, or `mapFinalCommand`:

```fsharp
let countLines : ProcessSpec =
    cmd $"printf 'error one\nerror two\n'"
    => cmd $"wc -l"
```

```fsharp run
countLines |> run |> stdoutOf |> _.Trim() |> shouldEqual "2"
```

Configure downstream stages inside parentheses before connecting them:

```fsharp
open Axial.Process.DSL

let shout (value: string) : ProcessSpec =
    cmd $"printf %%s {value}"
    => (cmd $"tr '[:lower:]' '[:upper:]'"
        |> timeout (TimeSpan.FromSeconds 5.0))
```

The interpolated value is one argument, even with spaces or shell characters in it:

```fsharp run
shout "hello; world" |> run |> stdoutOf |> shouldEqual "HELLO; WORLD"
```

The same configured pipeline can use qualified functions throughout:

```fsharp
let shoutWithFunctions (value: string) : ProcessSpec =
    Process.command $"printf %%s {value}"
    |> Process.pipe (Process.command $"tr '[:lower:]' '[:upper:]'"
                     |> Process.timeout (TimeSpan.FromSeconds 5.))
```

```fsharp run
shoutWithFunctions "hello; world" |> Process.run |> stdoutOf |> shouldEqual "HELLO; WORLD"
```

Each interpolation hole becomes one argument. Use [`secret`](xref:M:Axial.Process.DSL.secret) when plans, failures, and transcripts must show `***` instead of the real value. Use [`cmdText`](xref:M:Axial.Process.DSL.cmdText) only for fixed command text.

Use [`Process.commandArgs`](xref:M:Axial.Process.Process.commandArgs) when arguments already exist as a list. It has the same immutable
specification result, without command-line parsing.

[`Process.plan`](xref:M:Axial.Process.Process.plan) returns a redacted, serializable description without executing anything. [`Process.render`](xref:M:Axial.Process.Process.render) returns a redacted shell-like diagnostic string; it is not a shell command generator.

Shared fallback settings belong on the topology. Explicit command settings override them; defaults also apply to
stages connected later. `Process.plan` includes the resolved settings and their provenance.

```fsharp
let defaults =
    StageDefaults.empty
    |> StageDefaults.environment "CI" "true"
    |> StageDefaults.timeout (TimeSpan.FromSeconds 15.)

let sharedPipeline : ProcessSpec =
    cmd $"printf ready"
    |> stageDefaults defaults
    |> pipeTo (cmd $"cat" |> withoutTimeout)
```

```fsharp run
sharedPipeline |> run |> stdoutOf |> shouldEqual "ready"
```

Use `mapFinalCommand` to change settings on the final command of an existing topology. It requires one
final command and rejects executable or argument replacement. An explicitly routed stdout cannot also feed a pipe;
configure the downstream command's route, or use a tee at a readable external channel.

For checked examples of stdin, file routes, tee, fan-in, parallel capture, and recovery, see [DSL recipes](dsl-recipes.html).

## Build from fixed text or argument lists

`cmdText` parses fixed text without invoking a shell; shell operators are ordinary arguments. `pipe` accepts a
list of interpolated command templates when a pipeline is assembled dynamically. Use `Process.commandArgs`
when arguments already exist as individual values.

```fsharp run
cmdText "printf fixed" |> run |> stdoutOf |> shouldEqual "fixed"
pipe [ $"printf 'one\ntwo\n'"; $"wc -l" ] |> run |> stdoutOf |> _.Trim() |> shouldEqual "2"
Process.commandArgs "printf" ["%s"; "space and ; characters"]
|> run |> stdoutOf |> shouldEqual "space and ; characters"
```
