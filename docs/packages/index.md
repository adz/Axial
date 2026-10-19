---
title: Packages
---

# Packages

Supporting packages are documented with Axial Core unless they provide an independently useful programming model.

## Axial Core

`Axial`

Typed asynchronous workflows with explicit dependencies and expected failures, plus concurrency, resources, streams, and scheduling.

[Read the Core documentation →](/)

## Axial.Process

`Axial.Process`

Composable external processes for F#: commands, pipelines, streaming output, typed failures, cancellation, and cleanup.

[Read the Process documentation →](/process/)

## Axial.HttpClient

`Axial.HttpClient`

Typed HTTP workflows with request construction, response handling, reliability policies, and Axial integration.

[Read the HTTP documentation →](/http/)

## Supporting packages and platform compatibility

| Package | Provides | First use |
| --- | --- | --- |
| `Axial.PlatformService` | Clock, logging, randomness, GUID, and environment-variable services. | [Platform services](/services/platform-services/) |
| `Axial.Console` | The console service (`IConsole`). | [Console](/services/console.html) |
| `Axial.FileSystem` | The file-system service (`IFileSystem`). | [File system](/services/filesystem.html) |
| `Axial.Files` | Streaming file traversal and text reading. | [Files](/services/files.html) |
| `Axial.WebSocket` | Typed WebSocket client workflows. | [WebSocket](/services/websocket.html) |
| `Axial.Layers` | Reusable environment provisioning. | [Layers](/layers/) |
| `Axial.Hosting` | .NET and Microsoft Generic Host integration. | [Platforms and hosting](/platforms-and-hosting/) |
| `Axial.Hosting.Node` | Node.js signals, arguments, exit code, and environment. | [Node](/platforms-and-hosting/node.html) |
| `Axial.Hosting.Browser` | Browser ownership and `AbortSignal` integration. | [Browser](/platforms-and-hosting/browser.html) |
| `Axial.ElmishFlows` | Elmish commands backed by Axial workflows. | [Elmish](/platforms-and-hosting/elmish.html) |
| `Axial.Telemetry` | .NET tracing and OpenTelemetry integration. | [Observability](/observability/) |
| `Axial.Telemetry.JavaScript` | Fable JavaScript span export. | [JavaScript telemetry](/observability/telemetry/javascript.html) |
| `Axial.Guardrails` | Build-time analyzer that flags direct ambient effects. | [Guardrails](/notes/guardrails.html) |

See [Packages and platforms](/notes/packages-and-platforms.html) for the complete package and runtime matrix.
