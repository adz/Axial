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
| `Axial.PlatformService` | Yes | Yes | Yes | Clock, logging, randomness, GUID, and environment-variable services; live behavior follows the host. |
| `Axial.Console` | Yes | Yes | Yes | Console service; JavaScript behavior is limited to facilities supplied by the host console. |
| `Axial.FileSystem` | Yes | API only | API only | The live filesystem implementation is .NET-only. Supply a JavaScript implementation explicitly if needed. |
| `Axial.HttpClient` | Yes | API only | API only | Request plans are portable; the live `HttpClient` implementation is .NET-only. |
| `Axial.Process` | Yes | API only | API only | Process plans are portable; live child-process execution is .NET-only. |
| `Axial.Hosting` | Yes | No | No | Standalone .NET and Microsoft Generic Host integration. |
| `Axial.Hosting.Node` | No | Yes | No | Node signals, arguments, exit code, and `process.env`. |
| `Axial.Hosting.Browser` | No | No | Yes | Browser ownership and `AbortSignal` integration. |
| `Axial.Telemetry` | Yes | No | No | .NET `System.Diagnostics` and OpenTelemetry integration. |
| `Axial.Telemetry.JavaScript` | No | Yes | Yes | Fable JavaScript span export; configure an exporter appropriate to the host. |

JavaScript-only hosting packages intentionally fail outside their named runtime. The browser adapter does not treat page
visibility or unload as dependable application shutdown.
