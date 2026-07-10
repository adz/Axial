---
weight: 35
title: Processes And Pipelines
description: Launch safely tokenized commands, compose OS pipelines, and stream output through Flow.
type: docs
---


This page shows how to write shell-like process pipelines while keeping arguments tokenized, failures typed, cancellation
cooperative, and the process capability visible in `Flow`'s environment.

Bash is excellent process notation, but a shell command is one string whose quoting, expansion, and exit behavior are
mostly runtime concerns. `Axial.Flow.Process` keeps the useful left-to-right shape and makes the boundaries explicit.

## Bash And Flow Side By Side

```bash
git log --format=%an |
  sort |
  uniq -c |
  sort -nr |
  head -n 5
```

```fsharp
open Axial.Flow.Process

let topAuthors =
    Process.command "git" [ "log"; "--format=%an" ]
    |>> Process.command "sort" []
    |>> Process.command "uniq" [ "-c" ]
    |>> Process.command "sort" [ "-nr" ]
    |>> Process.command "head" [ "-n"; "5" ]
    |> Process.run
```

The Flow version is only slightly longer, and each argument is a distinct F# value. There is no quoting language to
get right and no accidental interpolation of `;`, `$()`, spaces, or wildcards. `topAuthors` has the type:

```fsharp
Flow<#IHas<IProcess>, ProcessError, ProcessResult>
```

Nothing starts until the flow runs. `|>>` only builds an immutable `Pipeline`; `Process.run` requests `IProcess` from
the environment and performs the effect. The live service starts every stage, connects the actual OS streams, and
runs the stages concurrently rather than buffering one complete command before starting the next.

## Commands Are Data

Build a base command and derive variants with ordinary F# pipelines:

```fsharp
let test project configuration =
    Process.command "dotnet" [ "test"; project; "--configuration"; configuration ]
    |> Process.workingDirectory "/work/repo"
    |> Process.environment "CI" "true"
    |> Process.pipeline
    |> Process.run
```

Use `Process.arg` when an argument is conditional, `Process.removeEnvironment` to suppress an inherited variable, and
`Process.input` to write text to the first command's stdin. A value such as `"hello world"` remains one argument. Do
not pre-quote it.

Do not use this API when you specifically need shell syntax such as redirection, globbing, variable expansion, or
compound commands. Invoke the shell explicitly (`Process.command "sh" [ "-c"; script ]`) at that boundary, and treat
the script as code rather than user input.

## Exit Codes And Complete Output

`Process.run` succeeds only when every stage exits with code zero. A failure is
`ProcessError.ExitedNonZero result`, so diagnostics are not lost:

```fsharp
type ProcessResult =
    { ExitCode: int
      ExitCodes: int list
      StdOut: string
      StdErr: string }
```

`ExitCode` is the last stage's code. `ExitCodes` contains every stage from left to right, which catches failures that
Bash normally hides without `set -o pipefail`. `StdOut` is the final stage's output. `StdErr` combines stderr from all
stages; stderr is never fed into the next command.

When a non-zero exit is expected data, use `Process.runResult`. Startup failures, cancellation, and process I/O still
use the typed `ProcessError` channel.

## Stream While Capturing

Long-running tools should report progress before they finish. `runStreaming` invokes an asynchronous observer for each
chunk while still returning the complete final `ProcessResult`:

```fsharp
let printOutput = function
    | ProcessOutput.StdOut text -> task { printf "%s" text }
    | ProcessOutput.StdErr(stage, text) -> task { eprintf "[%d] %s" stage text }

let build =
    Process.command "dotnet" [ "build"; "--nologo" ]
    |> Process.pipeline
    |> Process.runStreaming printOutput
```

The observer returns `Task<unit>`, so slow consumers apply backpressure instead of creating an unbounded event queue.
`runResultStreaming` is the corresponding variant for callers that interpret exit codes themselves. Flow cancellation
terminates running child processes and returns `ProcessError.Canceled`.

## Provide The Capability

Production code normally provisions `Process.layer`; tests provide a small `IProcess` implementation that inspects the
typed `Pipeline` or returns deterministic output.

```fsharp
let program =
    topAuthors
    |> Flow.provide Process.layer

let exit = Flow.runSync () program
```

Keep command construction in workflow code and the live implementation at the application edge. This preserves the
same explicit-service pattern as filesystem, HTTP, console, and other Flow packages.
