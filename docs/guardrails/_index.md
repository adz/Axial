---
title: Guardrails
linkTitle: Guardrails
description: Detect ambient effects, exceptions in workflows, and unsafe concurrency with Axial.Guardrails and the axial CLI.
---

# Guardrails

`Axial.Guardrails` is an FSharp.Analyzers.SDK analyzer package plus the `axial` CLI used to review policy. Install it
in the projects that should enforce effect boundaries:

```bash
dotnet add package Axial.Guardrails
```

The analyzer runs during compilation. `axial check`, `axial policy review`, and `axial policy approve` manage the
repository policy, baseline, and lock.

## Guides

1. [Effect-boundary guardrails](/guardrails/guardrails.html): policy, roles, rules, and daily workflow.
2. [Diagnostics](diagnostics.html): every `AXG` rule and how to resolve it.
3. [Rule authoring](rule-authoring.html): write and test an analyzer rule.
4. [Writing effect analyzers](/guardrails/writing-effect-analyzers.html): build an analyzer for a new effect boundary.
5. [Analyzer packaging](/guardrails/analyzer-packaging.html): package and publish analyzers and their build props.

## Reference

The generated Guardrails API reference covers the analyzer and policy surface. The `Axial.Tool` package provides the
`axial` command; the [diagnostics](diagnostics.html) page lists every rule and its resolution.
