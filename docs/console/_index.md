---
title: Console
linkTitle: Console
description: Standard streams, redirection, terminal control, and a recording test double.
project: src/Axial.Console/Axial.Console.fsproj
---

# Console

`Axial.Console` replaces `System.Console` with an explicit service. Install it and open `Axial.Console`:

```bash
dotnet add package Axial.Console
```

```fsharp
open Axial.Console
```

`IConsole` covers the three standard streams, redirection and encoding state, and interactive terminal control.
`Console.live` provides the live implementation, while `Console.test` records writes for tests.

## Guides

1. [Using the console service](/console/console.html): reads, writes, redirection, terminal control, and test doubles.

## Reference

The generated [Console API](xref:M:Axial.Console.Console.live) lists the service operations and live/test
implementations.
