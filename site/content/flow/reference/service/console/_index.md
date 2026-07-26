---
title: "Services Console"
weight: 20
type: docs
---

This page shows the console service package. `IConsole` models standard input and output as an explicit workflow service. Keep business logic typed against the service contract, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output.

## Service

- [`Flow.Console.IConsole`](./t-flow-console-iconsole.md): Provides explicit access to standard console and terminal I/O.

## Helpers

- [`Flow.Console.readLine`](./m-flow-console-console-readline.md):
- [`Flow.Console.writeLine`](./m-flow-console-console-writeline.md):
- [`Flow.Console.live`](./p-flow-console-console-live.md): Creates a live console service backed by <a href="https://learn.microsoft.com/dotnet/api/system.console">Console</a>.
- [`Flow.Console.layer`](./p-flow-console-console-layer.md): Builds the live console service as a layer.
