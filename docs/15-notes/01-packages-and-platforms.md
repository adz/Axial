---
title: Packages and platforms
linkTitle: Packages and platforms
description: Flow product packages and their supported .NET, Node, and browser runtimes.
---

# Packages and platforms

The table distinguishes host-neutral Fable JavaScript from packages that bind a specific runtime. “API only” means
portable plans and service contracts may compile there, but the package does not provide a live implementation for that
runtime.

| Package | .NET | Fable JavaScript: Node | Fable JavaScript: browser | Notes |
| --- | --- | --- | --- | --- |
| `Axial` | Yes | Yes | Yes | Core workflows, runtime, layers, concurrency, streams, schedules, and `App`. |
| `Axial.Layers` | Yes | Yes | Yes | Reusable provisioning: build an environment with flow capabilities, then provide it. |
| `Axial.PlatformService` | Yes | Yes | Yes | Clock, logging, randomness, GUID, and environment-variable services; live behavior follows the host. |
| `Axial.Console` | Yes | Yes | Yes | Console service; JavaScript behavior is limited to facilities supplied by the host console. |
| `Axial.FileSystem` | Yes | API only | API only | The live filesystem implementation is .NET-only. Supply a JavaScript implementation explicitly if needed. |
| `Axial.HttpClient` | Yes | API only | API only | Request plans are portable; the live `HttpClient` implementation is .NET-only. |
| `Axial.Process` | Yes | API only | API only | Process plans are portable; live child-process execution is .NET-only. |
| `Axial.Hosting` | Yes | No | No | Standalone .NET and Microsoft Generic Host integration. |
| `Axial.Hosting.Node` | No | Yes | No | Node signals, arguments, exit code, and `process.env`. |
| `Axial.Hosting.Browser` | No | No | Yes | Browser ownership and `AbortSignal` integration. |
| `Axial.Hosting.AspNetCore` (experimental) | Yes | No | No | ASP.NET Core schema-driven endpoints; depends on Reified packages. |
| `Axial.Hosting.GenHttp` | Yes | No | No | GenHTTP schema-driven endpoints; depends on Reified packages. |
| `Axial.Telemetry` | Yes | No | No | .NET `System.Diagnostics` and OpenTelemetry integration. |
| `Axial.Telemetry.JavaScript` | No | Yes | Yes | Fable JavaScript span export; configure an exporter appropriate to the host. |
| `Axial.Guardrails` | — | — | — | Build-time FSharp.Analyzers.SDK analyzer (`net8.0`). Runs during compilation of any project, including Fable ones, to flag direct ambient effects; not a runtime dependency. |

JavaScript-only hosting packages intentionally fail outside their named runtime. The browser adapter does not treat page
visibility or unload as dependable application shutdown.

## Dependencies

NuGet installs the transitive dependencies below; reference only the packages you use directly. Every package also depends on `FSharp.Core`.

`Axial` defines the service contracts, such as `IClock`, but does not depend on `Axial.PlatformService`. That package adds the live clock, logging, randomness, GUID, and environment-variable implementations.

| Package | Axial packages it brings in | Other dependencies |
| --- | --- | --- |
| `Axial` | None | `Fable.Core` |
| `Axial.Layers` | `Axial` | None |
| `Axial.PlatformService` | `Axial`, `Axial.Layers` | None |
| `Axial.Console` | `Axial` | None |
| `Axial.FileSystem` | `Axial` | None |
| `Axial.HttpClient` | `Axial`, `Axial.PlatformService` | None |
| `Axial.Process` | `Axial`, `Axial.PlatformService`, `Axial.Console`, `Axial.FileSystem` | None |
| `Axial.Hosting` | `Axial`, `Axial.Layers`, `Axial.PlatformService`, `Axial.HttpClient` | `Microsoft.Extensions.Configuration.Abstractions`, `DependencyInjection.Abstractions`, `Hosting.Abstractions`, `Http`, `Logging.Abstractions` |
| `Axial.Hosting.Node` | `Axial`, `Axial.PlatformService` | `Fable.Core` |
| `Axial.Hosting.Browser` | `Axial` | `Fable.Core` |
| `Axial.Telemetry` | `Axial` | `System.Diagnostics.DiagnosticSource` |
| `Axial.Telemetry.JavaScript` | `Axial` | `Fable.Core` |
| `Axial.Hosting.AspNetCore` (experimental) | `Axial` | `Reified.Schema.Http`, `Reified.Schema`, `Reified.Data` |
| `Axial.Hosting.GenHttp` | `Axial` | `GenHTTP.Api`, `GenHTTP.Modules.IO`, `Reified.Schema.Http`, `Reified.Schema`, `Reified.Data` |

Installing `Axial.Process` also installs `Axial.Console`, `Axial.FileSystem`, and `Axial.PlatformService`.
Only the two HTTP host adapters, `Axial.Hosting.AspNetCore` (experimental) and `Axial.Hosting.GenHttp`, depend on Reified. The core
`Axial` package and the operational packages do not.
