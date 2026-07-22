---
weight: 2
title: Installation
description: Install Axial.Flow and open its namespace.
type: docs
---


Flow is independent from the `Axial` package. Install it directly:

```sh
dotnet add package Axial.Flow
```

Open its namespace where workflows are defined:

```fsharp
open Axial.Flow
```

`Axial.Flow` contains the workflow type, `flow {}` builder, runtime, structured concurrency, schedules, and
application lifecycle.

Platform services and hosts are separate packages. Add one only when the application needs that integration:

```sh
dotnet add package Axial.Flow.HttpClient
dotnet add package Axial.Flow.Hosting
```

See [Packages and Platforms]({{< relref "/flow/packages-and-platforms/" >}}) for the complete package map.
