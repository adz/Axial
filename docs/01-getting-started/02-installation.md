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

The package configures itself. Builds run in **Fast** mode by default: a syntax-only pass over changed files, in
process, with no second typecheck. Fast reports guidance as warnings or hints and never fails an ordinary build on
it. Guardrails infrastructure failures (`AXGINF001`) and invalid policy files still fail the build.

The policy lives in checked-in files at the repository root. Install the `axial` command from the `Axial.Tool`
package, as a local tool so the repository pins its version, then create the policy files:

```sh
dotnet new tool-manifest   # once per repository, if it has no .config/dotnet-tools.json
dotnet tool install Axial.Tool
dotnet axial init
```

`axial init` creates `Axial.Guardrails.toml` (the policy: project and file roles and per-role rule levels),
`Axial.Guardrails.baseline` (reviewed existing findings) and `Axial.Guardrails.lock` (the reviewed snapshot). It only
creates missing files and never overwrites one. Commit all three. With a global install
(`dotnet tool install --global Axial.Tool`) the command is `axial` instead of `dotnet axial`.

Two commands check the repository:

- `axial check` compares the policy with the lock. Drift prints a warning and asks for `axial policy review`
  rather than failing; a corrupt lock or unreadable policy fails. It also prints each project's raw-Task trend from its latest build.
- `axial check --full` is the checkpoint for CI and releases. It checks that the policy review has no unreviewed
  loosening, then builds the solution in **Full** mode: every typed check, every project and target framework. It
  fails on Error findings, skipped sources, or incomplete coverage.

Use `dotnet build -p:AxialGuardrailsMode=Full` to run the typed checks in an ordinary build. For an existing
codebase, `axial policy baseline --rule <id>` prepares baseline entries for current findings; a person reviews them
and runs `axial policy approve`.

Keep intentional effects in files the policy assigns to the `host` or `capabilities` role. To omit a
package-specific rule set entirely, remove its `AxialGuardrail` MSBuild item; this disables that rule set rather than
changing finding severity. Roles, rule levels, baselines and suppressions are documented in
[Effect-boundary guardrails](/guardrails/guardrails.html).

See [Effect-boundary guardrails](/guardrails/guardrails.html) for the diagnostic list, configuration options, and
suppression syntax.

## Related guides

- [Hosting](/hosting/dotnet.html) chooses between standalone .NET, Generic Host, Node, and browser
  entry points.
- [Platform services](/services/platform-services/index.html) introduces the explicit clock, logging, random, GUID,
  culture, time-zone, and formatting services. Configuration lives in the [Configuration set](/configuration/).
- [Effect-boundary guardrails](/guardrails/guardrails.html) is the analyzer that enforces those contracts as a build
  check instead of a review convention.
