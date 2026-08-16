---
title: Effect-boundary guardrails
linkTitle: Effect-boundary guardrails
description: Use Axial.Guardrails to detect ambient effects, exceptions in workflows, shared test fixtures, and discarded cancellation tokens.
---

# Effect-boundary guardrails

Axial models operational effects as explicit, mockable dependencies. Direct access to ambient state can bypass those dependencies without changing a function's signature.

For example, `Schedule.jittered` once created its own `System.Random`, and `FiberDump` once read `DateTimeOffset.UtcNow` directly. Both errors compiled because the build did not enforce the effect boundary.

`Axial.Guardrails` is an [FSharp.Analyzers.SDK](https://ionide.github.io/FSharp.Analyzers.SDK/) analyzer package. It checks effect boundaries and related Axial conventions during each build.

## Install the analyzer

Add the package to your project:

```bash
dotnet add package Axial.Guardrails
```

The package configures the analyzer automatically. You don't need to edit an MSBuild file or run a separate command.

Findings are warnings by default. To make findings fail the build, set the severity to `error`:

```xml
<PropertyGroup>
  <AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>
</PropertyGroup>
```

To disable the analyzer for one project, set:

```xml
<PropertyGroup>
  <AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>
</PropertyGroup>
```

For adoption guidance, see [Installation](/getting-started/installation.html#add-effect-boundary-guardrails-optional).

## Run the analyzer in the Axial repository

Axial's `Directory.Build.targets` runs the local analyzer for every project. It imports the same targets file that the NuGet package provides, but points it at the local build output.

As a result, `dotnet build` and `dotnet test` use the same checks in this repository and in consumer projects. No separate script or CI task is required.

The repository sets `AxialGuardrailsSeverity` to `error`. To change the severity for one command, set the `AXIAL_GUARDRAILS_SEVERITY` environment variable.

## Review the diagnostics

| Code | Name | Detects |
| --- | --- | --- |
| `AXG001` | `EffectBoundary` | Direct calls to ambient .NET effects |
| `AXG002` | `SuppressionIntegrity` | Invalid or unused effect suppressions |
| `AXG003` | `RaiseInFlow` | Direct exception-raising calls inside `flow { }` |
| `AXG004` | `Fixture` | Shared module-level values in xUnit test modules |
| `AXG005` | `DiscardedCancellation` | Task adapters that discard their cancellation token |

`AXG001`, `AXG002`, `AXG003`, and `AXG005` run in application and library projects. They don't run in test projects, where direct effects and exceptions are often necessary for setup and assertions.

`AXG004` runs only in projects where MSBuild sets `IsTestProject`. It checks a test-specific risk and does not apply to application or library projects.

## AXG001: Use explicit effect services

`AXG001` detects direct calls to ambient .NET effects.

| Category | Detects | Use instead |
| --- | --- | --- |
| `random` | Construction of `System.Random` | `IRandom`, `Random.service`, or `Random.nextDouble` |
| `guid` | `Guid.NewGuid()` | `IGuid`, `Guid.service`, or `Guid.newGuid` |
| `clock` | Ambient date and time properties, and `Task.Delay` | `IClock`, `Clock.service`, `Clock.utcNow`, `Flow.sleep`, or `Schedule` |
| `environment` | Ambient environment variables and machine or process properties | `IEnvironment` or a value passed through `'env` |
| `console` | Any `System.Console` member | `Axial.Console.IConsole` |
| `filesystem` | Any `System.IO.File` or `System.IO.Directory` member | `Axial.FileSystem.IFileSystem` |
| `process` | `System.Diagnostics.Process.Start` | `Axial.Process.IProcess` |
| `sleep` | `Thread.Sleep` | `Flow.sleep` or `Schedule` |

The analyzer matches resolved `System.*` symbols, not source text. It does not flag an application type named `Random` or a local value named `now`.

### Allow an intentional effect boundary

Some code implements the explicit boundary around an effect. Examples include a live service implementation, a process entry point, and the scheduler.

To allow one call, add a category-specific directive to the flagged line or the line immediately above it:

```fsharp
let live : IClock =
    { new IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow } // axial-allow-effect: clock
```

To allow a category throughout a boundary implementation file, place a file directive in the leading comment block immediately before the `namespace` declaration:

```fsharp
// This file provides the live IConsole implementation.
// axial-allow-effect-file: console
```

A file directive allows only the named categories. For example, a `console` directive does not allow `System.Random()`.

Each directive must name a category. There is no directive that allows every effect.

For the exact matching and suppression rules, see `EffectCatalog.fs` and `Suppressions.fs` in `src/Axial.Guardrails`.

## AXG002: Remove invalid suppressions

`AXG002` validates every `axial-allow-effect` and `axial-allow-effect-file` directive.

It reports a directive when the category is unknown or when the directive does not suppress an `AXG001` finding. This check catches spelling errors and directives left behind after a refactoring.

Remove an unused directive. Correct a category only when the associated call is an intentional effect boundary.

## AXG003: Return typed failures from workflows

A `Flow<'env, 'error, 'value>` represents expected failures through its `'error` channel. Raising an exception inside `flow { }` creates an unhandled defect instead, so callers cannot handle it as an expected error.

`AXG003` detects direct calls to `raise`, `failwith`, `failwithf`, `invalidOp`, `invalidArg`, and `reraise` inside `flow { }`. It also checks nested conditionals, matches, bindings, and lambdas.

Use `return! Flow.fail error` for an expected failure. Use `Flow.die` when the failure is an unrecoverable defect.

If a nearby `try/with` catches and translates the exception, add `axial-allow-raise` to the flagged line or the line immediately above it:

```fsharp no-check reason="Illustrative fragment; proc is defined by the surrounding application"
if not (proc.Start()) then raise (Exception "Process did not start") // axial-allow-raise
```

Use this directive only when the same local boundary catches and translates the exception.

## AXG004: Create fresh test fixtures

A module-level `let` value is initialized once and shared by every test in the module. Mutable or stateful values can therefore make parallel xUnit tests interfere with each other.

`AXG004` checks modules that contain an xUnit `[<Fact>]` or `[<Theory>]`, or an FsCheck `[<Property>]`. It reports module-level values but not functions such as `let fixture () = ...`.

The analyzer excludes constants and point-free function definitions because they do not create the shared-state risk.

Create fixtures inside each test or expose a function that returns a fresh fixture. For a safe immutable value, add `axial-allow-fixture` to the flagged line or the line immediately above it:

```fsharp
let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) // axial-allow-fixture
```

## AXG005: Preserve cancellation tokens

Task adapters receive Flow's cancellation token. Discarding that token prevents cancellation from stopping the underlying operation.

`AXG005` checks `ColdTask`, `ColdTask.create`, `Flow.fromTask`, and `Flow.fromTaskResult`. It reports a single-argument lambda when the argument is a bare discard, such as `fun _ -> ...`.

Use the cancellation-aware overload of the wrapped operation and pass the token to it.

If a legacy API has no cancellation-aware overload, add `axial-allow-discarded-cancellation` to the flagged line or the line immediately above it:

```fsharp no-check reason="Illustrative fragment; legacyCall represents an API without cancellation support"
let legacy = ColdTask(fun _ -> legacyCall ()) // axial-allow-discarded-cancellation
```

This check does not run in test projects. Test adapters often wrap completed tasks that have no work to cancel.
