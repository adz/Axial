---
title: Platform Services
description: Explicit clock, logging, randomness, environment, console, and file-system capabilities for Axial workflows.
---

# Platform services

Platform services expose common host capabilities to `Flow` with cancellation, replaceable implementations, and typed errors where callers can recover. They are supporting parts of Axial Core rather than separate documentation contexts.

Higher-level libraries build their own programming models on top of Axial and these capabilities. [Axial.Process](/process/) composes external commands and streams. [Axial.HttpClient](/http/) models HTTP requests, responses, and reliability. Each has its own guides and API reference.

| Service | Wraps | Axial API |
| --- | --- | --- |
| [Clock](platform-services/clock.html) | `DateTimeOffset.UtcNow` | Current time and derived Unix-time readers |
| [Logging](platform-services/logging.html) | Host logging | Structured messages and exception-preserving logging |
| [Randomness and GUIDs](platform-services/random-and-guid.html) | `System.Random`, `Guid.NewGuid()` | Explicit random values, byte generation, and GUID creation |
| [Environment variables](platform-services/environment-variables.html) | `Environment.GetEnvironmentVariable` | Required and parsed reads with `EnvironmentVariableError` |
| [Console](console.html) | `System.Console` | Standard streams, redirection, and terminal control |
| [File system](filesystem.html) | `System.IO.File` and `Directory` | File and directory operations with `FileSystemError` |

```fsharp
open Axial
open Axial.Console
```

A workflow declares the capability it needs:

```fsharp
let greet name : Flow<#IHasConsole, Never, unit> =
    Console.writeLine $"Hello, {name}."
```

The host supplies the live implementation at the edge. Tests supply a recording or in-memory implementation of the same contract.

## In this section

1. [Platform-service bundle](platform-services/index.html): clock, logging, randomness, GUIDs, and environment variables.
2. [Console](console.html): standard streams, redirection, and terminal control.
3. [File system](filesystem.html): files, directories, paths, and typed failures.
4. [Using existing services](existing-services.html): compose `BaseRuntime` with application dependencies.

[Telemetry](/observability/telemetry/index.html) instruments the runtime rather than defining a workflow dependency. [Hosting](/platforms-and-hosting/index.html) supplies environments and application lifecycle integration.
