---
title: Installation
description: Install Axial and open its namespace.
---

# Installation

`Axial` is the package you install. `Flow<'env, 'error, 'value>` is the type it gives you, and `flow { }` is the
computation expression that builds values of that type. The documentation says "Flow" when it means the workflow
model and "Axial" when it means the library or a package name.

Install the core package:

```sh
dotnet add package Axial
```

Open the namespace where you define workflows:

```fsharp
open Axial
```

The package contains the workflow type, the `flow { }` builder, the runtime, structured concurrency, schedules, and
the application lifecycle.

Platform services and hosts ship as separate packages. Add one only when your application needs that integration:

```sh
dotnet add package Axial.HttpClient
dotnet add package Axial.Hosting
```

See [Packages and Platforms](/notes/packages-and-platforms.html) for the complete package map.

## Go further

- [Hosting](/platforms-and-hosting/dotnet.html) chooses between standalone .NET, Generic Host, Node, and browser
  entry points.
- [Platform services](/services/platform-services/index.html) introduces the explicit clock, logging, random,
  GUID, and environment-variable contracts.
