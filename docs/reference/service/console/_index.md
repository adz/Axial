---
title: "Services Console"
weight: 20
---

This page shows the console service package. `IConsole` models standard input and output as an explicit workflow service. Keep business logic typed against the service contract, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output.

## Service

- [`Console.IConsole`](./t-console-iconsole.md): Provides synchronous access to standard console I/O.

## Helpers

- [`Console.Console.readLine`](./m-console-console-readline.md): Reads a line through an explicit console service.
- [`Console.Console.writeLine`](./m-console-console-writeline.md): Writes a line through an explicit console service.
- [`Console.Console.live`](./p-console-console-live.md): Creates a live console service backed by <a href="https://learn.microsoft.com/dotnet/api/system.console">Console</a>.
- [`Console.Console.layer`](./p-console-console-layer.md): Builds the live console service as a layer.

