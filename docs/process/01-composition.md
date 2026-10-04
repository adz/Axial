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
let status (repository: string) =
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

Connect stdout to the next stage with `=>` (or the pipeline helper `pipeTo`):

```fsharp
let countLines : Flow<ProcessHostEnvironment, ProcessError, ProcessResult> =
    cmd $"printf 'error one\nerror two\n'"
    => cmd $"wc -l"
    |> toFlow
```

```fsharp run
stdoutOf countLines |> _.Trim() |> shouldEqual "2"
```

Configure downstream stages inside parentheses before connecting them:

```fsharp
open Axial.Process.DSL

let shout (value: string) =
    cmd $"printf %%s {value}"
    => (cmd $"tr '[:lower:]' '[:upper:]'"
        |> timeout (TimeSpan.FromSeconds 5.0))
    |> toFlow
```

The interpolated value is one argument, even with spaces or shell characters in it:

```fsharp run
stdoutOf (shout "hello; world") |> shouldEqual "HELLO; WORLD"
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

let sharedPipeline =
    cmd $"printf ready"
    |> stageDefaults defaults
    |> pipeTo (cmd $"cat" |> withoutTimeout)
```

```fsharp run
sharedPipeline |> toFlow |> stdoutOf |> shouldEqual "ready"
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
cmdText "printf fixed" |> toFlow |> stdoutOf |> shouldEqual "fixed"
pipe [ $"printf 'one\ntwo\n'"; $"wc -l" ] |> toFlow |> stdoutOf |> _.Trim() |> shouldEqual "2"
Process.commandArgs "printf" ["%s"; "space and ; characters"]
|> toFlow |> stdoutOf |> shouldEqual "space and ; characters"
```
