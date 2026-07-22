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

## Go Further

- [Hosting]({{< relref "/flow/hosting/" >}}) chooses between standalone .NET, Generic Host, Node, and browser entry points.
- [Platform services]({{< relref "/flow/platform-service/" >}}) introduces the explicit clock, logging, random,
  GUID, and environment-variable contracts.
