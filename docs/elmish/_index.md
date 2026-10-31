---
title: Elmish
linkTitle: Elmish
description: Connect Elmish 5 programs to Axial workflows with commands, subscriptions, and a test runtime.
project: src/Axial.ElmishFlows/Axial.ElmishFlows.fsproj
---

# Elmish

`Axial.ElmishFlows` connects an Elmish 5 program to Axial workflows. Install it and open `Axial.ElmishFlows`:

```bash
dotnet add package Axial.ElmishFlows
```

```fsharp
open Axial.ElmishFlows
```

Commands and subscriptions run as Axial flows with the program's runtime services. `ElmishRuntime.start` supplies the
`Post` and defect callbacks, and `TestRuntime` drives commands in tests.

## Guides

1. [Elmish programs](/elmish/elmish.html): runtime setup, commands, subscriptions, history, and testing.

## Reference

The generated [ElmishFlows API](xref:M:Axial.ElmishFlows.ElmishRuntime.start) covers commands, subscriptions, inboxes,
the runtime, and the test harness.
