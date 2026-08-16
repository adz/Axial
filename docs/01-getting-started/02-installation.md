---
title: Installation
description: Install Axial and open its namespace.
---

# Installation

Install the `Axial` package to use `Flow<'env, 'error, 'value>` and the `flow { }` computation expression.

This documentation uses "Flow" for the workflow model and "Axial" for the library or a package name.

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

## Add effect-boundary guardrails (optional)

A workflow can bypass its declared dependencies by reading ambient state such as `DateTime.Now` or by creating
`System.Random` directly. `Axial.Guardrails` detects these calls and related Axial usage errors during the build.

Install the analyzer package:

```sh
dotnet add package Axial.Guardrails
```

The package configures the analyzer automatically. Findings are warnings by default, so you can add it to an
existing project without failing the build.

Review each finding. Replace accidental ambient access with an explicit service. Add a category-specific
suppression only when the call implements an intentional effect boundary.

After resolving the initial findings, configure the analyzer to fail the build on new findings:

```xml
<PropertyGroup>
  <AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>
</PropertyGroup>
```

To disable the analyzer for one project without removing the package, set
`AxialGuardrailsEnabled` to `false`.

See [Effect-boundary guardrails](/notes/guardrails.html) for the diagnostic list, configuration options, and
suppression syntax.

## Go further

- [Hosting](/platforms-and-hosting/dotnet.html) chooses between standalone .NET, Generic Host, Node, and browser
  entry points.
- [Platform services](/services/platform-services/index.html) introduces the explicit clock, logging, random,
  GUID, and environment-variable contracts.
- [Effect-boundary guardrails](/notes/guardrails.html) is the analyzer that enforces those contracts as a build
  check instead of a review convention.
