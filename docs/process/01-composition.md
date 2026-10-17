---
title: Commands and composition
description: Build immutable process specifications and connect native streams.
---

A `ProcessCommand` describes a native process; it does not start one. [`cmd`](xref:M:Axial.Process.DSL.cmd) builds it from an
interpolated template and tokenizes that template safely: each interpolation hole becomes exactly one native
argument:

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

`Process.run` executes a single command and returns its result:

```fsharp run
cmd $"printf hello" |> Process.run |> _.StdOut |> shouldEqual "hello"
```

To check another repository, `cwd` runs Git there. `env "GIT_OPTIONAL_LOCKS" "0"` skips optional locks and index
refreshes for a status check, and `timeout` bounds the wait. None of the settings start a process; each returns
another `ProcessCommand`:

```fsharp
let status (repository: string) : ProcessCommand =
    cmd $"git status --short"
    |> cwd repository
    |> env "GIT_OPTIONAL_LOCKS" "0"
    |> timeout (TimeSpan.FromSeconds 15.0)
```

These functions take a `ProcessCommand`, so set them before joining the stage into a `ProcessSpec`. When a reusable
function has already returned a complete topology, [`Process.mapFinalCommand`](xref:M:Axial.Process.Process.mapFinalCommand)
lets a caller adjust settings on its final command without rebuilding the pipeline. It preserves that command's
executable and arguments; use it for caller-specific settings such as a deadline, not to change what program runs.
Qualified helpers such as [`Process.arg`](xref:M:Axial.Process.Process.arg), [`Process.secretArg`](xref:M:Axial.Process.Process.secretArg),
[`Process.removeEnvironment`](xref:M:Axial.Process.Process.removeEnvironment),
[`Process.encoding`](xref:M:Axial.Process.Process.encoding), and
[`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) also take a `ProcessCommand`.

## Connect native process stages

A shell can run this pipeline as one Bash program:

```fsharp
let countLinesInShell =
    bash $"printf 'error one\\nerror two\\n' | wc -l"
```

The `=>` operator builds the same topology from two command descriptions: the left command's stdout becomes the
right command's stdin. Each stage remains a separate value in the `ProcessSpec`; no shell parses the pipeline.
That means each command can have its own working directory, environment, timeout, and success-code policy, and
`Process.plan` can show the connected stages before execution. Use `bash` when shell syntax itself is what you need;
use `=>` when you want Axial to compose and manage native process stages.

```fsharp
let countLines : ProcessSpec =
    cmd $"printf 'error one\\nerror two\\n'"
    => cmd $"wc -l"
```

Both forms count the same two lines:

```fsharp run
let shellCount = countLinesInShell |> Process.run |> _.StdOut.Trim()
let composedCount = countLines |> Process.run |> _.StdOut.Trim()
shouldEqual "2" shellCount
shouldEqual shellCount composedCount
```

Parentheses let a downstream stage carry its own settings before it is connected:

```fsharp
open Axial.Process.DSL

let shout (value: string) : ProcessSpec =
    cmd $"printf %%s {value}"
    => (cmd $"tr '[:lower:]' '[:upper:]'"
        |> timeout (TimeSpan.FromSeconds 5.0))
```

The interpolated value is one argument, even with spaces or shell characters in it:

```fsharp run
shout "hello; world" |> Process.run |> _.StdOut |> shouldEqual "HELLO; WORLD"
```

The same configured pipeline can use qualified functions throughout:

```fsharp
let shoutWithFunctions (value: string) : ProcessSpec =
    Process.command $"printf %%s {value}"
    |> Process.pipe (Process.command $"tr '[:lower:]' '[:upper:]'"
                     |> Process.timeout (TimeSpan.FromSeconds 5.))
```

```fsharp run
shoutWithFunctions "hello; world" |> Process.run |> _.StdOut |> shouldEqual "HELLO; WORLD"
```

## Interpolation keeps each value in one argument

Values from your program belong in interpolation holes. `cmd` and `Process.command` pass each whole value as one
native argument, so spaces and characters such as `;`, `|`, and `$` stay literal — neither runs a shell. The
`shout` example above passes `"hello; world"` to `printf` unchanged before piping it into `tr`.

Use [`secret`](xref:M:Axial.Process.DSL.secret) when plans and diagnostics must show `***` instead of the real
argument. Use [`cmdText`](xref:M:Axial.Process.DSL.cmdText) for fixed command text that should not be parsed.
For shell syntax, see [scripts and shell syntax](scripts.html).

Use [`Process.commandArgs`](xref:M:Axial.Process.Process.commandArgs) when arguments already exist as separate
values. It skips command-line parsing and produces the same immutable command.

[`Process.plan`](xref:M:Axial.Process.Process.plan) returns a redacted, serializable description without executing anything. [`Process.render`](xref:M:Axial.Process.Process.render) returns a redacted, shell-like diagnostic string for humans; it is not a shell command generator.

Settings shared by a whole pipeline belong on the topology rather than repeated per command. Explicit command
settings override them, and defaults reach stages connected later. `Process.plan` shows the resolved settings
and where each came from.

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
sharedPipeline |> Process.run |> _.StdOut |> shouldEqual "ready"
```

`mapFinalCommand` is useful when the pipeline is reusable but a caller owns the policy for its final stage. For
example, add a deadline after the topology has been assembled:

```fsharp
let countLinesWithDeadline =
    countLines
    |> Process.mapFinalCommand (Process.timeout (TimeSpan.FromSeconds 5.0))
```

```fsharp run
countLinesWithDeadline |> Process.run |> _.StdOut.Trim() |> shouldEqual "2"
```

This transforms the unique final command's settings. It rejects changing its executable or arguments, and cannot
apply to a topology with multiple final commands. Prefer configuring a stage before connecting it when the setting is
intrinsic to that stage. A stdout route and a pipe are mutually exclusive: configure the downstream command's route,
or tee to a readable external channel.

For checked examples of stdin, file routes, tee, fan-in, parallel capture, and recovery, see [DSL recipes](dsl-recipes.html).

## Build from fixed text or argument lists

`cmdText` parses fixed text without invoking a shell, so shell operators become ordinary arguments. `pipe` builds
a pipeline from a list of interpolated templates when the stages are assembled dynamically.

```fsharp run
cmdText "printf fixed" |> Process.run |> _.StdOut |> shouldEqual "fixed"
pipe [ $"printf 'one\ntwo\n'"; $"wc -l" ]
|> Process.run |> _.StdOut.Trim() |> shouldEqual "2"
Process.commandArgs "printf" ["%s"; "space and ; characters"]
|> Process.run |> _.StdOut |> shouldEqual "space and ; characters"
```
