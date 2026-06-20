---
title: "Services Console"
weight: 20
---

This page shows the console service package. `IConsole` models standard input and output as an explicit workflow service. Keep business logic typed against the service contract, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output.

## Service

- [`Flow.Console.IConsole`](./t-flow-console-iconsole.md): Provides synchronous access to standard console I/O.

## Helpers

- [`Flow.Console.Console.readLine`](./m-flow-console-console-readline.md): Reads a line through an explicit console service.
- [`Flow.Console.Console.writeLine`](./m-flow-console-console-writeline.md): Writes a line through an explicit console service.
- [`Flow.Console.Console.live`](./p-flow-console-console-live.md): Creates a live console service backed by <a href="https://learn.microsoft.com/dotnet/api/system.console">Console</a>.
- [`Flow.Console.Console.layer`](./p-flow-console-console-layer.md): Builds the live console service as a layer.
