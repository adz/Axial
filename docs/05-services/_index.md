---
title: Built-in Services
description: The console, file-system, and process services Axial ships, and how to supply them.
---

Axial ships implementations for the ambient capabilities most applications reach for: the console, the file system,
and external processes. Each one is an ordinary explicit dependency. Nothing here is a new mechanism — these pages
apply [explicit services](/dependencies/explicit-services.html) and [layers](/dependencies/layers.html) to
capabilities the library already wrote for you.

Every built-in service follows the same three-part shape:

| Part | Purpose |
| --- | --- |
| `IConsole`, `IFileSystem`, `IProcess` | The contract a workflow depends on through `IHas<'service>` |
| `Console.live`, `FileSystem.live`, `Process.live` | The implementation backed by the real platform |
| `Console.layer`, `FileSystem.layer`, `Process.layer` | The same implementation as a `Layer` for runtime composition |

A workflow names the contract and never the implementation:

```fsharp
let greet name : Flow<#IHas<IConsole>, Never, unit> =
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

1. [Console](console.html) — standard streams, redirection, and terminal control.
2. [FileSystem](filesystem.html) — files, directories, paths, and typed `FileSystemError` values.
3. [Processes](processes/index.html) — external commands as composable, typed workflows.

Other capabilities Axial provides have sections of their own: [HTTP](/http/index.html),
[platform services](/platforms-and-hosting/platform-services.html) such as the clock, and
[telemetry](/observability/telemetry/index.html).
