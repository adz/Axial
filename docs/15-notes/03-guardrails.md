---
title: Effect-boundary guardrails
linkTitle: Effect-boundary guardrails
description: Use Axial.Guardrails to detect ambient effects, exceptions in workflows, shared test fixtures, discarded cancellation tokens, and resources returned from fibers.
---

# Effect-boundary guardrails

Axial models operational effects as explicit, mockable dependencies. Direct access to ambient state can bypass those dependencies without changing a function's signature.

`Schedule.jittered` once created its own `System.Random`, and `FiberDump` once read `DateTimeOffset.UtcNow` directly. Both compiled because the build did not enforce the effect boundary.

`Axial.Guardrails` is an [FSharp.Analyzers.SDK](https://ionide.github.io/FSharp.Analyzers.SDK/) analyzer package. It checks effect boundaries and related Axial conventions during each build.

## Install the analyzer

Add the package to your project:

```bash
dotnet add package Axial.Guardrails
```

The package configures the analyzer without MSBuild edits or a separate command.
Which rules run depends on the Axial packages the project references; see [Choose guardrails per package](#choose-guardrails-per-package).

Findings are warnings by default. With error severity, the build shows the `AXG` findings and fails with
`Axial.Guardrails reported findings above (see AXG codes).` A different analyzer CLI exit code reports a tool failure
instead; neither case is reported as an `MSB3073` command failure.

To make findings fail the build, set the severity to `error`:

```xml
<PropertyGroup>
  <AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>
</PropertyGroup>
```

To downgrade findings to non-failing warnings during a migration, without editing each project, set
`AXIAL_GUARDRAILS_SEVERITY=warning` in the environment, or pass `-p:AxialGuardrailsSeverity=warning` to `dotnet build`
or `dotnet test`. A severity set in a project or with `-p:` wins over the environment variable.

To disable the analyzer for one project, set:

```xml
<PropertyGroup>
  <AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>
</PropertyGroup>
```

For adoption guidance, see [Installation](/getting-started/installation.html#add-effect-boundary-guardrails-optional).

### Verify the analyzer runs

A minimal deliberate finding confirms that the package actually runs and fails at error severity. In a consumer
project that references `Axial.Guardrails` and `Axial`, put this in a source file:

```fsharp no-check reason="Deliberate finding in a separate consumer project, not part of the docs build"
module Consumer
let now () = System.DateTimeOffset.UtcNow
```

Build with `<AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>` set (or `AXIAL_GUARDRAILS_SEVERITY=error` in
the environment). The build exits non-zero and reports the finding:

```text
error AXG001: This call reads the ambient system clock through System.DateTimeOffset, which makes timing untestable.
The Axial guardrail asks for an explicit, mockable alternative: use Axial.IClock (Axial.PlatformService.Clock.now).
```

The same source built with `AXIAL_GUARDRAILS_SEVERITY=warning` succeeds and reports `warning AXG001` instead. If
you see neither form, the analyzer is not running in that project: check the package reference and that the guardrail
for the effect's package is enabled (the core `Axial` guardrail covers clock reads).

## Review the diagnostics

| Code | Name | Detects |
| --- | --- | --- |
| `AXG001` | `EffectBoundary` | Direct calls to ambient .NET effects |
| `AXG002` | `SuppressionIntegrity` | Invalid or unused effect suppressions |
| `AXG003` | `RaiseInFlow` | Direct exception-raising calls inside `flow { }` |
| `AXG004` | `Fixture` | Shared module-level values in xUnit test modules |
| `AXG005` | `DiscardedCancellation` | Task adapters that discard their cancellation token |
| `AXG006` | `ReflectionFormatting` | Formatting that needs F# reflection, which NativeAOT and trimming remove |
| `AXG007` | `ResourceFromFiber` | A forked flow that returns a scoped resource it acquired, which the fiber releases before `Fiber.join` returns it |
| `AXG008` | `ScopeFromRun` | A run returning a scope-owned service that closes when the run ends |

`AXG001`, `AXG002`, `AXG003`, `AXG005`, `AXG006`, `AXG007`, and `AXG008` run in application and library projects. They don't run in test projects, where direct effects and exceptions are often necessary for setup and assertions. Set `AxialGuardrailsInTests` to `all` to run them in a test project too.

`AXG004` runs only in projects where MSBuild sets `IsTestProject`. It checks a test-specific risk and does not apply to application or library projects.

## Choose guardrails per package

The rules are grouped into guardrails, each named after the package whose service replaces the effect it flags.
Referencing a package turns its guardrail on: a rule that recommends `IHttp` is only useful once `IHttp` is available.

| Guardrail | Rules |
| --- | --- |
| `Axial` | Ambient clock reads, `Stopwatch`, `Task.Delay`, `Thread.Sleep`, `Environment.ProcessorCount`, blocking waits, and `AXG002`–`AXG008` |
| `Axial.PlatformService` | Randomness, GUIDs, environment state, and `TimeZoneInfo.Local` |
| `Axial.Console` | `System.Console` |
| `Axial.FileSystem` | `System.IO.File` and `System.IO.Directory` |
| `Axial.Process` | `Process.Start` |
| `Axial.HttpClient` | `new HttpClient` |

Change the set with ordinary MSBuild item operations in a project or `Directory.Build.props`:

```xml
<ItemGroup>
  <!-- Turn a guardrail off -->
  <AxialGuardrail Remove="Axial.Console" />
  <!-- Give one guardrail its own severity; the others use AxialGuardrailsSeverity -->
  <AxialGuardrail Update="Axial.HttpClient" Severity="warning" />
  <!-- Turn one on without referencing its package -->
  <AxialGuardrail Include="Axial.FileSystem" />
</ItemGroup>
```

### Add rules for your own package

A package can contribute rules to a guardrail of its own without writing a separate analyzer, by shipping
`<AxialGuardrailRule>` items in its `buildTransitive` props. The
[rule-authoring reference](guardrail-rule-authoring.html) documents the `AxialGuardrailRule` metadata and the `Match`
grammar.

## Diagnostic reference

Each diagnostic's contract — what it detects, what to use instead, and the suppression directive that allows an
intentional boundary — is on the [diagnostics reference](guardrail-diagnostics.html):

- [`AXG001`](guardrail-diagnostics.html#axg001-use-explicit-effect-services): ambient .NET effects.
- [`AXG002`](guardrail-diagnostics.html#axg002-remove-invalid-suppressions): invalid or unused suppressions.
- [`AXG003`](guardrail-diagnostics.html#axg003-return-typed-failures-from-workflows): exceptions raised in `flow { }`.
- [`AXG004`](guardrail-diagnostics.html#axg004-create-fresh-test-fixtures): shared module-level test fixtures.
- [`AXG005`](guardrail-diagnostics.html#axg005-preserve-cancellation-tokens): task adapters that drop their token.
- [`AXG006`](guardrail-diagnostics.html#axg006-format-without-reflection): reflection-dependent formatting.
- [`AXG007`](guardrail-diagnostics.html#axg007-keep-resources-acquired-by-a-fiber): resources returned from fibers.
- [`AXG008`](guardrail-diagnostics.html#axg008-keep-scope-owned-services-inside-their-run): scope-owned services returned by a run.
