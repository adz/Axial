---
title: Installation
description: Install Axial and open its namespace.
---

# Installation

Flow is independent from the `Axial` package. Install it directly:

```sh
dotnet add package Axial
```

Open its namespace where workflows are defined:

```fsharp
open Axial
```

`Axial` contains the workflow type, `flow {}` builder, runtime, structured concurrency, schedules, and
application lifecycle.

Platform services and hosts are separate packages. Add one only when the application needs that integration:

```sh
dotnet add package Axial.HttpClient
dotnet add package Axial.Hosting
```

See [Packages and Platforms](/notes/packages-and-platforms.html) for the complete package map.

## Go Further

- [Hosting](/platforms-and-hosting/dotnet.html) chooses between standalone .NET, Generic Host, Node, and browser entry points.
- [Platform services](/services/platform-services/index.html) introduces the explicit clock, logging, random,
  GUID, and environment-variable contracts.
