---
title: Get started with Guardrails
linkTitle: Get started
description: Install Axial.Guardrails, create the policy files, and check that the analyzer reports a finding.
---

# Get started with Guardrails

## Install

Add the analyzer to each F# project it should check:

```bash
dotnet add package Axial.Guardrails
```

The package sets itself up. It runs on every `dotnet build` and `dotnet test`.

Install the `axial` command as a local tool, so the repository pins its version, and create the policy files:

```bash
dotnet new tool-manifest   # once, if the repository has no .config/dotnet-tools.json
dotnet tool install Axial.Tool
dotnet axial init
```

`axial init` asks which CI host you use (GitHub, GitLab, Bitbucket or Other) and creates three files at the
repository root. Commit all three:

| File | Holds |
| --- | --- |
| `Axial.Guardrails.toml` | The policy: which role each project and file has, and how strict each rule is. |
| `Axial.Guardrails.baseline` | Findings you accepted when adopting Guardrails. Starts empty. |
| `Axial.Guardrails.lock` | The policy as a person last reviewed it. |

`init` only creates missing files. It never overwrites one. Pass `--ci github|gitlab|bitbucket|other` to skip the
question. With a global install (`dotnet tool install --global Axial.Tool`) the command is `axial` rather than
`dotnet axial`.

## A minimal policy

This policy treats all code as domain code, where raw effects are errors, and names the files that implement
services:

```toml
version = 2
baseline = "Axial.Guardrails.baseline"

[default]
role = "domain"

[files]
services = ["src/App/Services/**"]

[roles.domain.rules]
"effects.ambient" = "deny"
```

[The policy file](policy-file.html) explains roles and levels.

## See a finding

Put this in a project that references `Axial.Guardrails` and `Axial`:

```fsharp no-check reason="Deliberate finding in a separate consumer project, not part of the docs build"
module Consumer
let now () = System.DateTimeOffset.UtcNow
```

The build fails with:

```text
error raw-effect: This call reads the ambient system clock through System.DateTimeOffset, which makes timing untestable.
The Axial guardrail asks for an explicit, mockable alternative: use Axial.IClock (Clock.now).
```

No finding means the analyzer is not running in that project. Check the package reference, and that the guardrail for
the effect's package is on: the core `Axial` guardrail covers clock reads ([Guardrails per package](per-package.html)).

## Day to day

- `dotnet build` runs the [Fast check](fast-and-full.html): quick advice on the files you changed.
- `dotnet axial check` says whether the policy changed since its last review, and how much raw `Task` code each
  project still has.
- `dotnet axial check --full` is the check for CI and releases. It runs every rule on every project and fails on
  errors.

When the policy loosens, a person reads `axial policy review` and runs `axial policy approve`
([Policy review](policy-review.html)). For a codebase that already has findings, see
[Adopt with a baseline](baseline.html).
