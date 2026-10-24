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

The package configures the analyzer automatically. New findings are errors. Review each finding and replace
accidental ambient access with an explicit service. For an existing codebase, generate and review a baseline so
existing findings remain visible as warnings while later findings fail the build.

A category-specific suppression is valid only for an intentional effect boundary in an explicitly declared file.
To omit a package-specific category entirely, remove its `AxialGuardrail` MSBuild item; this disables that rule set
rather than changing finding severity. Guardrail selection, baseline adoption, and the local troubleshooting switch
are documented in [Effect-boundary guardrails](/notes/guardrails.html).

See [Effect-boundary guardrails](/notes/guardrails.html) for the diagnostic list, configuration options, and
suppression syntax.

## Related guides

- [Hosting](/platforms-and-hosting/dotnet.html) chooses between standalone .NET, Generic Host, Node, and browser
  entry points.
- [Platform services](/services/platform-services/index.html) introduces the explicit clock, logging, random, secure-random,
  GUID, and environment-variable contracts.
- [Effect-boundary guardrails](/notes/guardrails.html) is the analyzer that enforces those contracts as a build
  check instead of a review convention.
