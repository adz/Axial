---
title: Packages
---

# Packages

Supporting packages are documented with Axial Core unless they provide an independently useful programming model.

## Axial Core

`Axial`

Maturity: Incubating

Typed asynchronous workflows with explicit dependencies and expected failures, plus concurrency, resources, streams, and scheduling.

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

## Supporting packages and platform compatibility

| Package | Maturity | Provides | First use |
| --- | --- | --- | --- |
| `Axial.PlatformService` | Incubating | Clock, logging, ordinary and secure randomness, GUID, and environment-variable services. | [Platform services](/services/platform-services/) |
| `Axial.Console` | Incubating | The console service (`IConsole`). | [Console](/services/console.html) |
| `Axial.FileSystem` | Incubating | The file-system service (`IFileSystem`). | [File system](/services/filesystem.html) |
| `Axial.Files` | Incubating | Streaming file traversal and text reading. | [Files](/services/files.html) |
| `Axial.WebSocket` | Experimental | Typed WebSocket client workflows. | [WebSocket](/services/websocket.html) |
| `Axial.Layers` | Incubating | Reusable environment provisioning. | [Layers](/layers/) |
| `Axial.Hosting` | Experimental | .NET and Microsoft Generic Host integration. | [Platforms and hosting](/platforms-and-hosting/) |
| `Axial.Hosting.Node` | Experimental | Node.js signals, arguments, exit code, and environment. | [Node](/platforms-and-hosting/node.html) |
| `Axial.Hosting.Browser` | Experimental | Browser ownership and `AbortSignal` integration. | [Browser](/platforms-and-hosting/browser.html) |
| `Axial.ElmishFlows` | Incubating | Elmish commands backed by Axial workflows. | [Elmish](/platforms-and-hosting/elmish.html) |
| `Axial.Telemetry` | Experimental | .NET tracing and OpenTelemetry integration. | [Observability](/observability/) |
| `Axial.Telemetry.JavaScript` | Experimental | Fable JavaScript span export. | [JavaScript telemetry](/observability/telemetry/javascript.html) |
| `Axial.Guardrails` | Incubating | Build-time analyzer that flags direct ambient effects. | [Guardrails](/notes/guardrails.html) |

See [Package maturity](/notes/package-maturity.html) for evidence and promotion criteria.

See [Packages and platforms](/notes/packages-and-platforms.html) for the complete package and runtime matrix.
