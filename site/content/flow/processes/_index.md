---
weight: 100
title: Axial.Flow.Process
linkTitle: Process
description: Build typed commands, connect process endpoints, stream output, and run executable F# scripts.
type: docs
---


This page shows the shortest path from Bash process syntax to an explicitly tracked Flow workflow.

```bash
git log --format=%an | sort | uniq -c | sort -nr | head -n 5
```

```fsharp
open Axial.Flow.Process
open Axial.Flow.Process.DSL

let topAuthors =
    cmd $"git log --format=%%an"
    => cmd $"sort"
    => cmd $"uniq -c"
    => cmd $"sort -nr"
    => cmd $"head -n 5"
    |> capture
```

Interpolation holes are argument values, not shell fragments. `=>` connects typed endpoints; `capture` converts the
completed topology to `Flow<#IHas<IProcess>, ProcessError, ProcessResult>`. The Flow runtime performs the effects.

For several stages, the collection form is easy to scan and supports normal F# conditionals and loops:

```fsharp
pipe [
    $"git log --format=%%an"
    $"sort"
    $"uniq -c"
    $"sort -nr"
    $"head -n {count}"
]
|> capture
```

Axial starts every stage concurrently and connects bounded byte streams. Unlike Bash without `pipefail`, a failed
upstream stage reports its command, index, exit code, duration, and bounded stderr tail.

## Mental Model

1. `cmd` creates a safely tokenized command.
2. `=>`, `pipe`, `pipeTo`, `pipeBothTo`, and `mergeStderr` create process topology.
3. `stdin`, `stdout`, `stderr`, `cwd`, `env`, encoding, and success-code operations configure it.
4. `toFlow`, `capture`, `console`, `stream`, or an output endpoint converts it to Flow.

Capture is the default output policy, but constructing a command does not execute it. The terminal conversion remains
visible. `Script.run` or an application Flow runtime is the actual interpreter boundary.

## Choose A Guide

- [Commands and pipelines](commands-pipelines/): safe arguments, endpoints, stdin sources, shell escape hatches, and real pipes.
- [Output and streaming](output-streaming/): full capture, bounded tails, files, teeing, binary data, callbacks, and `FlowStream`.
- [Failures and transcripts](failures-transcripts/): accepted exit codes, stage failures, durations, and redacted diagnostics.
- [Executable scripts](scripts/): complete `dotnet fsi` shebang files with one-call Flow hosting.
- [Fable and Node](fable/): portable stream semantics, adapters, and the process-host boundary.
