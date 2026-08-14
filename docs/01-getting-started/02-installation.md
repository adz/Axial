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

## Add effect-boundary guardrails (optional)

Axial's workflows are only as honest as the code around them: a `Flow` that hides a `DateTime.Now` or a
`new Random()` inside a dependency looks pure but isn't. `Axial.Guardrails` is an analyzer that catches that —
direct calls to the clock, randomness, GUIDs, the console, the filesystem, environment variables, or the process —
anywhere it isn't explicitly named as an intended boundary.

```sh
dotnet add package Axial.Guardrails
```

That's the whole install. The package wires itself into your build the moment it's referenced — no MSBuild edits,
no separate CLI to learn. What happens next depends on how you want to work with it.

**Trialing it in an existing codebase.** Leave the defaults alone. Findings show up as build warnings, so nothing
that already builds today stops building tomorrow. Read through them at your own pace, add a
`// axial-allow-effect: <category>` comment at any call site that's a genuine, intended boundary, and move on.
When a project is clean, promote it to build-breaking:

```xml
<PropertyGroup>
  <AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>
</PropertyGroup>
```

Not interested yet? `<AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>` turns it off for that project, no
uninstall required.

**Driving the change with an LLM.** Set errors from the start instead of easing in — an agent works better against
a build that fails with a specific, fixable diagnostic than against warnings it can ignore:

```xml
<PropertyGroup>
  <AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>
</PropertyGroup>
```

Then point an agent at `dotnet build` and let it work the list: each `AXG001` finding names the exact call, the
service to route it through instead (`Axial.PlatformService`'s `IClock`, `IRandom`, `IGuid`, `IEnvironment`, or
the matching package for console/filesystem/process), and the suppression comment to use instead if the call site
turns out to be a genuine boundary. The build stays red until every finding is fixed or explicitly annotated, so
there's no ambiguity about when the pass is done.

See [Effect-boundary guardrails](/notes/guardrails.html) for the full list of what's checked and how suppression
comments work.

## Go further

- [Hosting](/platforms-and-hosting/dotnet.html) chooses between standalone .NET, Generic Host, Node, and browser
  entry points.
- [Platform services](/services/platform-services/index.html) introduces the explicit clock, logging, random,
  GUID, and environment-variable contracts.
- [Effect-boundary guardrails](/notes/guardrails.html) is the analyzer that enforces those contracts as a build
  check instead of a review convention.
