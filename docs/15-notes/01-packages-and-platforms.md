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
| `Axial` | Yes | Yes | Yes | Core workflows, runtime platform services (clock, logging, random, GUID and test helpers), layers, concurrency, streams, schedules, and `App`. |
| `Axial.Configuration` | Yes | Yes* | Yes* | Layered environment, command-line, JSON, browser and Microsoft configuration decoded by Reified, with redacted secrets and optional watching. *Fable source loading/merging works on Reified 0.12.0; typed `schema<...>`/`Config.load` decoding requires Reified 0.12.1. |
| `Axial.Console` | Yes | Yes | Yes | Console service; JavaScript behavior is limited to facilities supplied by the host console. |
| `Axial.FileSystem` | Yes | API only | API only | The live filesystem implementation is .NET-only. Supply a JavaScript implementation explicitly if needed. |
| `Axial.HttpClient` | Yes | API only | API only | Request plans are portable; the live `HttpClient` implementation is .NET-only. |
| `Axial.Process` | Yes | API only | API only | Process plans are portable; live child-process execution is .NET-only. |
| `Axial.Hosting` | Yes | No | No | Standalone .NET and Microsoft Generic Host integration. |
| `Axial.Hosting.Node` | No | Yes | No | Node signals, arguments, exit code, and `process.env`. |
| `Axial.Hosting.Browser` | No | No | Yes | Browser ownership and `AbortSignal` integration. |
| `Axial.Hosting.AspNetCore` (experimental) | Yes | No | No | ASP.NET Core schema-driven endpoints; depends on Reified packages. |
| `Axial.Hosting.GenHttp` (experimental) | Yes | No | No | GenHTTP schema-driven endpoints; depends on Reified packages. |
| `Axial.Telemetry` | Yes | No | No | .NET `System.Diagnostics` and OpenTelemetry integration. |
| `Axial.Telemetry.JavaScript` | No | Yes | Yes | Fable JavaScript span export; configure an exporter appropriate to the host. |
| `Axial.Guardrails` | — | — | — | Build-time FSharp.Analyzers.SDK analyzer (`net8.0`). Runs during compilation of any project, including Fable ones, to flag direct ambient effects; not a runtime dependency. |

Each guide installs the package it teaches at first use: [Configuration](/configuration/), [Console](/console/console.html), [FileSystem](/filesystem/filesystem.html) and [Files](/filesystem/files.html), [WebSocket](/websocket/websocket.html), [Process](/process/), [HttpClient](/http/), [Hosting](/hosting/) ([Node](/hosting/node.html) and [Browser](/hosting/browser.html) for Fable), [Telemetry](/telemetry/index.html), and [Elmish flows](/elmish/elmish.html).

JavaScript-only hosting packages intentionally fail outside their named runtime. The browser adapter does not treat page
visibility or unload as dependable application shutdown.

## Dependencies

NuGet installs the transitive dependencies below; reference only the packages you use directly. Every package also depends on `FSharp.Core`.

`Axial` defines the service contracts, such as `IClock`, and ships the live clock, logging, ordinary and secure randomness, GUID and test helpers. Their `Axial.PlatformService` namespaces remain in `Axial` for 0.12. The old `Axial.PlatformService` package is deleted; `Axial.Configuration` owns environment, argument, JSON, browser and Microsoft configuration.

| Package | Axial packages it brings in | Other dependencies |
| --- | --- | --- |
| `Axial` | None | `Fable.Core` |
| `Axial.Console` | `Axial` | None |
| `Axial.Configuration` | `Axial`, `Axial.FileSystem`, `Axial.Process` | `Fable.Core`, `Reified.Data`, `Reified.Schema`, `Reified.Refinements` |
| `Axial.FileSystem` | `Axial` | None |
| `Axial.HttpClient` | `Axial` | None |
| `Axial.Process` | `Axial`, `Axial.Console`, `Axial.FileSystem` | None |
| `Axial.Hosting` | `Axial`, `Axial.Configuration`, `Axial.HttpClient` | `Microsoft.Extensions.Configuration.Abstractions`, `DependencyInjection.Abstractions`, `Hosting.Abstractions`, `Http`, `Logging.Abstractions`, `Reified.Data` |
| `Axial.Hosting.Node` | `Axial` | `Fable.Core` |
| `Axial.Hosting.Browser` | `Axial` | `Fable.Core` |
| `Axial.Telemetry` | `Axial` | `System.Diagnostics.DiagnosticSource` |
| `Axial.Telemetry.JavaScript` | `Axial` | `Fable.Core` |
| `Axial.Hosting.AspNetCore` (experimental) | `Axial` | `Reified.Schema.Http`, `Reified.Schema`, `Reified.Data` |
| `Axial.Hosting.GenHttp` (experimental) | `Axial` | `GenHTTP.Api`, `GenHTTP.Modules.IO`, `Reified.Schema.Http`, `Reified.Schema`, `Reified.Data` |

Installing `Axial.Process` also installs `Axial.Console` and `Axial.FileSystem`.
`Axial.Configuration` depends on Reified.Data/Schema/Refinements, `Axial.Hosting` directly references Reified.Data, and the two HTTP host adapters also depend on Reified. The core `Axial` package and the other operational packages do not.
