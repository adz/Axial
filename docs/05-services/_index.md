---
title: Built-in Services
description: The clock, logging, console, file-system, process, and HTTP services Axial ships, and how to supply them.
---

Axial integrates seamlessly with ordinary .NET code — nothing stops a workflow from calling `File.ReadAllText` or
`new HttpClient()` directly. The built-in services exist for the capabilities most applications reach for often
enough to be worth wrapping: the clock and other operational services, the console, the file system, external
processes, and HTTP. Each one is an ordinary explicit dependency. Nothing here is a new mechanism — these pages
apply [service contracts](/dependencies/service-contracts.html) and [layers](/layers/index.html) to capabilities the
library already wrote for you.

Every built-in service is built in two layers:

1. **A direct wrap.** The service contract mirrors the .NET API it replaces — `IClock.UtcNow()`,
   `FileSystem.readAllText`, `Process.command` — so nothing you already know about the underlying capability stops
   applying. Wrapping it, rather than calling it ambiently, is what earns three things for free: a **typed error
   channel** in place of unclassified exceptions, **concurrency handling** through the Flow runtime instead of raw
   tasks, and a **mockable dependency** — every service ships a deterministic test double beside its `live`
   implementation, and the platform services also compose into [`BaseRuntime`](platform-services/index.html) as one
   bundle.
2. **An improved ergonomic API, where it earns its place.** Some services stop at the wrap — `Console` and `Clock`
   are thin enough that the .NET API was already the right shape. Others add real value on top: `FileSystem` and
   `HttpClient` replace a dozen exception types with one typed union each; `EnvironmentVariable` adds required,
   parsed reads over the raw string lookup; `Process` adds composable piping and secret redaction that
   `System.Diagnostics.Process` has no equivalent for.

| Service | Wraps | Ergonomic layer on top |
| --- | --- | --- |
| [Clock](platform-services/clock.html) | `DateTimeOffset.UtcNow` | Derived readers (`utcDateTime`, `unixTimeSeconds`, `unixTimeMilliseconds`) |
| [Logging](platform-services/logging.html) | An ambient logger | `logException` preserves the exception object instead of flattening it to a string; `live` defaults to a safe no-op |
| [Randomness and GUIDs](platform-services/random-and-guid.html) | `System.Random`, `Guid.NewGuid()` | `nextInt min max` bounds a value in one call; `Random.bytes count` allocates and fills in one step |
| [Environment variables](platform-services/environment-variables.html) | `Environment.GetEnvironmentVariable` | `EnvironmentVariable` module: required, parsed reads failing with `EnvironmentVariableError` instead of `null` or an exception |
| [Console](console.html) | `System.Console` | None — the wrap is the whole surface; failures are defects, not a typed channel |
| [FileSystem](filesystem.html) | `System.IO.File` / `Directory` | Every operation returns `FileSystemError` in place of a dozen exception types |
| [Processes](processes/index.html) | `System.Diagnostics.Process` | Composable piping, `secretArg` redaction, a DSL, typed timeouts and transcripts |
| [HTTP](http/index.html) | `HttpClient` | DSL builders with automatic URL-encoding, typed `HttpError`, per-request `timeout`, `retryTransient` with backoff |

```fsharp
open Axial
open Axial.Console
```

Every built-in service follows the same three-part shape:

| Part | Purpose |
| --- | --- |
| `IHasClock`, `IHasConsole`, `IHasFileSystem`, `IHasProcess`, `IHasHttp` | The contract a workflow constrains its environment with |
| `Console.live`, `FileSystem.live`, `Http.live` | The implementation backed by the real platform |
| `Layer.succeed Console.live`, `Layer.succeed FileSystem.live`, `Layer.succeed (Http.live …)` | The same implementation as a `Layer` for runtime composition |

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
[choosing an approach](/dependencies/choosing-an-approach.html). Depending on `IConsole` to print one line couples the workflow to a
terminal it may not have.

## In this section

1. [Platform services](platform-services/index.html) — clock, logging, randomness, GUIDs, and environment variables.
2. [Console](console.html) — standard streams, redirection, and terminal control.
3. [FileSystem](filesystem.html) — files, directories, paths, and typed `FileSystemError` values.
4. [Processes](processes/index.html) — external commands as composable, typed workflows.
5. [HTTP](http/index.html) — typed requests, decoded responses, and transient-failure retries.
6. [Tutorial: Composing built-in services](existing-services.html) — embedding `BaseRuntime` and your own
   dependencies in one environment.

Two related capabilities are documented elsewhere because they are not services in this sense.
[Telemetry](/observability/telemetry/index.html) is cross-cutting instrumentation of the runtime rather than a
dependency a workflow declares, and [hosting](/platforms-and-hosting/index.html) supplies environments instead of
being one.
