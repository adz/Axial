---
title: Built-in Services
description: The clock, logging, console, file-system, process, and HTTP services Axial ships, and how to supply them.
---

Axial ships implementations for the ambient capabilities most applications reach for: the clock and other
operational services, the console, the file system, external processes, and HTTP. Each one is an ordinary explicit
dependency. Nothing here is a new mechanism — these pages apply
[explicit services](/dependencies/explicit-services.html) and [layers](/dependencies/layers.html) to capabilities the
library already wrote for you.

```fsharp
open Axial
open Axial.Console
```

Every built-in service follows the same three-part shape:

| Part | Purpose |
| --- | --- |
| `IHasClock`, `IHasConsole`, `IHasFileSystem`, `IHasProcess`, `IHasHttp` | The contract a workflow constrains its environment with |
| `Console.live`, `FileSystem.live`, `Http.live` | The implementation backed by the real platform |
| `Console.layer`, `FileSystem.layer`, `Http.layer` | The same implementation as a `Layer` for runtime composition |

A workflow names the contract and never the implementation:

```fsharp
let greet name : Flow<#IHasConsole, Never, unit> =
    Console.writeLine $"Hello, {name}."
```

The host supplies `live` at the edge, and tests supply a recording or in-memory value implementing the same
interface. Because the requirement is in the type, neither one can be forgotten.

## When to declare your own instead

Use these services when you genuinely need the real capability. When a workflow only needs "somewhere to report
progress" or "the current configuration", declare a narrow application dependency instead — see
[dependencies](/dependencies/dependencies.html). Depending on `IConsole` to print one line couples the workflow to a
terminal it may not have.

## In this section

1. [Platform services](platform-services/index.html) — clock, logging, randomness, GUIDs, and environment variables.
2. [Console](console.html) — standard streams, redirection, and terminal control.
3. [FileSystem](filesystem.html) — files, directories, paths, and typed `FileSystemError` values.
4. [Processes](processes/index.html) — external commands as composable, typed workflows.
5. [HTTP](http/index.html) — typed requests, decoded responses, and transient-failure retries.

Two related capabilities are documented elsewhere because they are not services in this sense.
[Telemetry](/observability/telemetry/index.html) is cross-cutting instrumentation of the runtime rather than a
dependency a workflow declares, and [hosting](/platforms-and-hosting/index.html) supplies environments instead of
being one.
