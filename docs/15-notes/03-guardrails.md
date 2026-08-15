---
title: Effect-boundary guardrails
linkTitle: Effect-boundary guardrails
description: Axial.Guardrails, the analyzer that catches ambient effects (clock, randomness, GUIDs, console, filesystem, process, environment) reached directly instead of through an explicit service.
---

# Effect-boundary guardrails

Axial's architecture invariant requires operational effects to be explicit, mockable dependencies visible in a
signature. Code must not read them from an ambient global. This rule has been violated in practice:
`Schedule.jittered` constructed its own `System.Random`, and `FiberDump` rendering read `DateTimeOffset.UtcNow`
directly. Both changes compiled and passed review, because nothing checked for the violation.

`Axial.Guardrails` is an [FSharp.Analyzers.SDK](https://ionide.github.io/FSharp.Analyzers.SDK/) analyzer package
that enforces this rule, and four related rules, automatically. Reference the package and it wires itself into
your build. No script to run, no MSBuild file to edit.

## Install it

```bash
dotnet add package Axial.Guardrails
```

The check now runs as part of every `dotnet build`. By default, a finding is a build warning, so nothing that
builds today stops building tomorrow.

To fail the build on a finding instead, set the severity to `error`:

```xml
<PropertyGroup>
  <AxialGuardrailsSeverity>error</AxialGuardrailsSeverity>
</PropertyGroup>
```

To disable the check for a project, set:

```xml
<PropertyGroup>
  <AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>
</PropertyGroup>
```

See [Installation](/getting-started/installation.html#add-effect-boundary-guardrails-optional) for the two adoption
paths this supports: trialing it in an existing codebase, or driving a strict pass with an LLM agent.

## Axial's own repo uses the same install

Axial's `Directory.Build.targets` adds a build-order-only reference to `Axial.Guardrails` for every project in
this repo, then imports the same `Axial.Guardrails.targets` file the packaged NuGet gives external consumers,
pointed at the analyzer's local build output instead of a packaged copy. The result: `dotnet build` and
`dotnet test` run the identical check here as they do for anyone who adds the package. There's no separate
script and no separate CI job to keep in sync with the package's own behavior.

`Directory.Build.props` sets this repo's default to `AxialGuardrailsSeverity=error`, stricter than the package's
default of `warning`. Set the `AXIAL_GUARDRAILS_SEVERITY` environment variable to override it for one run, for
example while migrating a package onto explicit services.

## What it checks

| Code | Name | Flags |
| --- | --- | --- |
| `AXG001` | `EffectBoundary` | A direct call to an ambient .NET effect |
| `AXG002` | `SuppressionIntegrity` | A suppression comment that's mistyped or covers nothing |
| `AXG003` | `RaiseInFlow` | `raise`/`failwith` called directly inside `flow { }` |
| `AXG004` | `Fixture` | A shared, module-level `let` value in an xUnit test module |
| `AXG005` | `DiscardedCancellation` | A `ColdTask`/`Flow.fromTask` lambda that discards its `CancellationToken` |

`AXG001`-`AXG003` and `AXG005` run against application and library code. `AXG004` runs only against test
projects (detected through the `IsTestProject` MSBuild property that the test SDK sets), since it has nothing to
find anywhere else. `AXG001`-`AXG003` and `AXG005` are excluded from test projects for the same reason in
reverse: test code legitimately touches `Thread.Sleep`, `System.IO.File`, `DateTimeOffset`, and raw exceptions
for setup and assertions, so those checks would be noise there, not signal.

## AXG001: effect boundary

| Category | Flags | Use instead |
| --- | --- | --- |
| `random` | `System.Random()` construction | `Axial.PlatformService`'s `IRandom` (`Random.service`, `Random.nextDouble`) |
| `guid` | `Guid.NewGuid()` | `IGuid` (`Guid.service`, `Guid.newGuid`) |
| `clock` | `DateTime.Now`/`UtcNow`/`Today`, `DateTimeOffset.Now`/`UtcNow`, `Task.Delay` | `IClock` (`Clock.service`, `Clock.utcNow`), or `Flow.sleep`/`Schedule` |
| `environment` | `Environment.GetEnvironmentVariable`/`SetEnvironmentVariable`, `MachineName`, `UserName`, `OSVersion`, `ProcessorCount`, `CurrentDirectory` | `IEnvironment`, or an explicit configuration value passed through `'env` |
| `console` | Any `System.Console` member | `Axial.Console`'s `IConsole` |
| `filesystem` | Any `System.IO.File` or `System.IO.Directory` member | `Axial.FileSystem`'s `IFileSystem` |
| `process` | `System.Diagnostics.Process.Start` | `Axial.Process`'s `IProcess` |
| `sleep` | `Thread.Sleep` | `Flow.sleep`/`Schedule` |

Matching is symbol-based, against the resolved `System.*` member, not text-based. An application's own type named
`Random`, or a local binding named `now`, is never flagged.

### Mark an intended boundary

Some code is genuinely the explicit boundary an effect is supposed to live behind: a `live` service
implementation, a process entry point, the scheduler itself. Silence a finding there by naming the effect
category. A suppression can't accidentally cover an effect it wasn't written for.

To allow one call site, add a comment on the flagged line or the line directly above it:

```fsharp
let live : IClock =
    { new IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow } // axial-allow-effect: clock
```

To allow every call site in a file whose entire purpose is to be an effect's boundary, such as a service's `live`
implementation module, add a comment in the file's leading comment block, directly above the `namespace`
declaration:

```fsharp
// This file is the live IConsole implementation: its entire purpose is to be the explicit,
// mockable boundary around System.Console, so ambient console access here is intentional.
// axial-allow-effect-file: console
```

`axial-allow-effect-file` silences only the categories it names. A file allowed for `console` still gets flagged
for an unrelated `System.Random()` call. There's no blanket "allow everything" form. Every suppression states
which effect it covers, so a reviewer can tell at a glance whether it's the right one.

See `EffectCatalog.fs` and `Suppressions.fs` in `src/Axial.Guardrails` for the exact matching and suppression
rules.

## AXG002: suppression integrity

A suppression comment is an escape hatch, and escape hatches accumulate cruft over time: a category name typo'd
so it silences nothing (`// axial-allow-effect: guide` doesn't match `guid`), or a suppression left behind after
the call it once covered was refactored away or moved to a different line. Both leave a comment that looks like a
reviewed, intentional boundary, but isn't.

`AXG002` cross-references every `axial-allow-effect`/`axial-allow-effect-file` directive against the known
category list and against the actual `AXG001` findings it's positioned to cover. A mistyped or orphaned
suppression is flagged the same way an unmarked effect call is.

## AXG003: raise/failwith inside flow

A `Flow<'env, 'error, 'value>` already has a typed `'error` channel for expected failures. Calling
`raise`/`failwith`/`failwithf`/`invalidOp`/`invalidArg`/`reraise` directly inside a `flow { }` block is the most
common mistake when writing Axial code for the first time. It's the instinct carried over from ordinary F# and
C#, and it silently turns what should be an `'error` value into an unhandled defect (`Cause.Die`). A caller
matching on `'error` never sees it.

`AXG003` flags any of those calls found lexically inside a `flow { }` block, including inside a nested
`if`/`match`/`let`/lambda within it. Prefer `return! Flow.fail err`, or `Flow.die` if the failure genuinely is
meant to be an unrecoverable defect.

Mark a deliberate call — most often because a surrounding `try`/`with` inside the same block already converts
every exception into a typed error, making `raise` the local control-flow escape into that boundary — with a
comment on the flagged line or the line above it:

```fsharp no-check reason="Illustrative fragment; proc is a local Process value from surrounding context, not shown here"
if not (proc.Start()) then raise (Exception "...") // axial-allow-raise
```

This suppression has no category. Use it only when the raise is genuinely caught and translated nearby, not as a
shortcut past the finding.

## AXG004: shared xUnit fixtures

A module-level `let` binding with no parameters is a value, computed once. The module's static initializer runs
it a single time, and every test in the module shares the result. Under xUnit's parallel test execution, that's a
correctness hazard for anything mutable or stateful. `AGENTS.md`'s Test Authoring section already states the
rule in prose: "Do not define shared fixtures as module-level `let` values in xUnit test modules."

`AXG004` checks it mechanically: a module-level `let` value, not a function — `let f () = ...` is re-invoked per
call and isn't flagged — in a module that also contains an xUnit `[<Fact>]`/`[<Theory>]` or FsCheck `[<Property>]`
test. A bare constant (`let tolerance = 0.0001`) and a point-free function definition (`let f = function | ... ->
...`) are both excluded, since neither is the shared-mutable-state hazard this targets.

Mark a genuinely safe, immutable shared value with a comment on the flagged line or the line above it:

```fsharp
let private syntheticTime = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) // axial-allow-fixture
```

Otherwise, build the fixture inside each test, or expose it as a function so each caller gets a fresh value.

## AXG005: discarded CancellationToken

[Add Axial to an existing Task application](/getting-started/existing-task-application.html) already calls this
out: "Some legacy APIs do not accept a cancellation token. You can adapt one with `ColdTask(fun _ -> legacyCall
())`, but cancelling the Flow cannot stop the underlying operation." The code compiles and looks exactly like
every other adapter. The mistake shows up only as a hang or a leaked operation under cancellation, which is hard
to reproduce and easy to blame on something else.

`AXG005` flags `ColdTask`, `ColdTask.create`, `Flow.fromTask`, and `Flow.fromTaskResult` given a lambda whose
single `CancellationToken` parameter is a bare discard (`fun _ -> ...`). If the wrapped call has a
cancellation-aware overload, thread the token through instead.

Mark a genuine legacy API with no such overload with a comment on the flagged line or the line above it:

```fsharp no-check reason="Illustrative fragment; legacyCall is a placeholder for an unspecified legacy API"
let legacy = ColdTask(fun _ -> legacyCall ()) // axial-allow-discarded-cancellation
```

`AXG005` is one of the checks excluded from test projects: constructing a `ColdTask`/`Flow.fromTask` around an
already-completed `Task.FromResult(...)`, a common test-fixture shape, has nothing to cancel, so discarding the
token there is fine.
