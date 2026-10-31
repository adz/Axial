---
title: Packages
---

# Packages

Supporting packages are documented with Axial Core unless they provide an independently useful programming model.

## Axial Core

`Axial`

Maturity: Incubating

Typed asynchronous workflows with explicit dependencies and expected failures, scoped runtime platform services, concurrency, resources, layers, streams, and scheduling.

[Read the Core documentation →](/)

## Axial.Process

`Axial.Process`

Maturity: Incubating

Composable external processes for F#: commands, pipelines, streaming output, typed failures, cancellation, and cleanup.

[Read the Process documentation →](/process/)

## Axial.HttpClient

`Axial.HttpClient`

Maturity: Incubating

Typed HTTP workflows with request construction, response handling, reliability policies, and Axial integration.

[Read the HTTP documentation →](/http/)

## Axial.Configuration

`Axial.Configuration`

Maturity: Experimental

Layered environment, command-line, JSON, browser, and Microsoft configuration decoded by a Reified schema, with redacted secrets and optional file watching.

[Read the Configuration documentation →](/configuration/)

## Axial.Console

`Axial.Console`

Maturity: Incubating

Standard streams, redirection state, and terminal control as an explicit service.

[Read the Console documentation →](/console/)

## Axial.FileSystem

`Axial.FileSystem`

Maturity: Incubating

File and directory access with typed errors, plus streaming traversal, atomic writes, and the append log.

[Read the FileSystem documentation →](/filesystem/)

## Axial.WebSocket

`Axial.WebSocket`

Maturity: Experimental

Typed WebSocket client sessions, sends, receives, and errors.

[Read the WebSocket documentation →](/websocket/)

## Axial.Hosting

`Axial.Hosting`, `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, `Axial.Hosting.Node`, `Axial.Hosting.Browser`

Maturity: Experimental, except `Axial.Hosting` and `Axial.Hosting.Node` follow the matrix below

Host adapters for Generic Host, ASP.NET Core, GenHttp, Node, browsers, and service managers.

[Read the Hosting documentation →](/hosting/)

## Axial.ElmishFlows

`Axial.ElmishFlows`

Maturity: Incubating

Elmish 5 commands and subscriptions backed by Axial workflows.

[Read the Elmish documentation →](/elmish/)

## Axial.Telemetry

`Axial.Telemetry`, `Axial.Telemetry.JavaScript`

Maturity: Experimental

.NET and Fable JavaScript tracing, fiber spans, metrics, and dumps.

[Read the Telemetry documentation →](/telemetry/)

## Axial.Guardrails

`Axial.Guardrails`, `Axial.Tool`

Maturity: Incubating, except `Axial.Tool`

Build-time effect-boundary analysis and the `axial` policy CLI.

[Read the Guardrails documentation →](/guardrails/)

## Supporting packages and platform compatibility

| Package | Maturity | Provides | First use |
| --- | --- | --- | --- |
| `Axial.Configuration` | Experimental | Layered environment, command-line, JSON, browser and Microsoft configuration decoded by a Reified schema, with plain string reads, redacted secrets and optional file watching. | [Configuration](/configuration/configuration.html) |
| `Axial.PlatformService` | Removed | Deleted in 0.12. Clock, logging, random, GUID and test helpers ship in `Axial` with the `Axial.PlatformService` namespace retained; use `Axial.Configuration` for environment variables. | |
| `Axial.Console` | Incubating | The console service (`IConsole`). | [Console](/console/console.html) |
| `Axial.FileSystem` | Incubating | The file-system service (`IFileSystem`) plus streaming traversal, atomic writes, and the append log. | [File system](/filesystem/filesystem.html) · [Files](/filesystem/files.html) |
| `Axial.WebSocket` | Experimental | Typed WebSocket client workflows. | [WebSocket](/websocket/websocket.html) |
| `Axial.Hosting` | Experimental | .NET and Microsoft Generic Host integration. | [Hosting](/hosting/) |
| `Axial.Hosting.Node` | Experimental | Node.js signals, arguments, exit code, and environment. | [Node](/hosting/node.html) |
| `Axial.Hosting.Browser` | Experimental | Browser ownership and `AbortSignal` integration. | [Browser](/hosting/browser.html) |
| `Axial.ElmishFlows` | Incubating | Elmish commands backed by Axial workflows. | [Elmish](/elmish/elmish.html) |
| `Axial.Telemetry` | Experimental | .NET tracing and OpenTelemetry integration. | [Telemetry](/telemetry/) |
| `Axial.Telemetry.JavaScript` | Experimental | Fable JavaScript span export. | [JavaScript telemetry](/telemetry/javascript.html) |
| `Axial.Guardrails` | Incubating | Build-time analyzer that flags direct ambient effects. | [Guardrails](/guardrails/guardrails.html) |
| `Axial.Tool` | Experimental | The `axial` command for initializing, checking, and approving Guardrails policy. | [Guardrails](/guardrails/guardrails.html) |

See [Package maturity](/notes/package-maturity.html) for evidence and promotion criteria.

See [Packages and platforms](/notes/packages-and-platforms.html) for the complete package and runtime matrix.
