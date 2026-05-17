---
title: "Capabilities Console"
---

This page shows the console capability package. `IConsole` is a small app capability for workflows that need standard input or output without depending directly on `System.Console`. Use it at command-line boundaries, examples, and simple interactive tools. Keep business logic typed against the interface, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output.

## Capability

- [`Capabilities.Console.IConsole`](./t-capabilities-console-iconsole.md): Provides synchronous access to standard console I/O.

## Helpers

- [`Capabilities.Console.Console.readLine`](./m-capabilities-console-console-readline.md): Reads a line from the console environment.
- [`Capabilities.Console.Console.writeLine`](./m-capabilities-console-console-writeline.md): Writes a line to the console environment.
- [`Capabilities.Console.Console.live`](./m-capabilities-console-console-live.md): Creates a live console backed by <a href="https://learn.microsoft.com/dotnet/api/system.console">Console</a>.

