---
title: Effect-boundary guardrails
linkTitle: Effect-boundary guardrails
description: Axial.Guardrails, the analyzer that catches ambient effects (clock, randomness, GUIDs, console, filesystem, process, environment) reached directly instead of through an explicit service.
---

# Effect-boundary guardrails

Axial's architecture invariant is that operational effects are explicit, mockable dependencies visible in a
signature — never read from an ambient global. That rule has been violated in practice: `Schedule.jittered` used
to construct its own `System.Random`, and `FiberDump` rendering used to read `DateTimeOffset.UtcNow` directly.
Both compiled cleanly and passed review, because nothing checked for it.

`Axial.Guardrails` is an [FSharp.Analyzers.SDK](https://ionide.github.io/FSharp.Analyzers.SDK/) analyzer that
checks for it. It flags direct calls to a fixed set of ambient .NET effects, and requires an explicit, categorized
comment at any call site where that's genuinely the intended boundary.

## What it flags

| Category | Flags | Use instead |
| --- | --- | --- |
| `random` | `System.Random()` construction | `Axial.PlatformService`'s `IRandom` (`Random.service`, `Random.nextDouble`) |
| `guid` | `Guid.NewGuid()` | `IGuid` (`Guid.service`, `Guid.newGuid`) |
| `clock` | `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `Task.Delay` | `IClock` (`Clock.service`, `Clock.utcNow`), or `Flow.sleep`/`Schedule` |
| `environment` | `Environment.GetEnvironmentVariable`/`SetEnvironmentVariable`, `MachineName`, `UserName`, `OSVersion`, `ProcessorCount`, `CurrentDirectory` | `IEnvironment`, or an explicit config value threaded through `'env` |
| `console` | Any `System.Console` member | `Axial.Console`'s `IConsole` |
| `filesystem` | Any `System.IO.File` or `System.IO.Directory` member | `Axial.FileSystem`'s `IFileSystem` |
| `process` | `System.Diagnostics.Process.Start` | `Axial.Process`'s `IProcess` |
| `sleep` | `Thread.Sleep` | `Flow.sleep`/`Schedule` |

Matching is symbol-based (against the resolved `System.*` member), not text-based, so an application's own type
named `Random` or a local `now` binding is never flagged.

## Running it

```bash
dotnet tool restore   # once, to fetch the local fsharp-analyzers tool
bash scripts/run-guardrails.sh
```

The script builds `Axial.Guardrails`, then checks every `src/Axial*` project registered in
`Axial.slnx` plus `examples/Axial.Examples`. It runs in CI on every push and pull request (the
`guardrails` job in `.github/workflows/ci.yml`), so a new unmarked ambient-effect call site fails
the build automatically — the same as the analyzer originally being wired via
`FSharp.Analyzers.Build`'s MSBuild targets, without needing per-project MSBuild wiring.

To check a single project by hand, point the CLI at it directly:

```bash
dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj
dotnet tool run fsharp-analyzers \
  --project src/Axial.HttpClient/Axial.HttpClient.fsproj \
  --analyzers-path artifacts/bin/Axial.Guardrails/debug
```

### Turning findings into warnings, or opting a project out

`scripts/run-guardrails.sh` treats findings as build-failing errors by default. Set
`AXIAL_GUARDRAILS_SEVERITY=warning` to report findings without failing the run — useful while a
package is mid-migration onto explicit services and you don't want it blocking unrelated work:

```bash
AXIAL_GUARDRAILS_SEVERITY=warning bash scripts/run-guardrails.sh
```

To exclude a project from the check entirely, add its `.fsproj` path to
`scripts/guardrails-exclude.txt` with a comment explaining why. Prefer a narrow
`// axial-allow-effect: <category>` comment at the call site over this file — the exclude list is
for a project that can't reasonably be checked at all, not a way to skip reviewing a finding.

## Marking an intended boundary

A finding is a question, not necessarily a bug: some code — a `live` service implementation, a process entry
point, the scheduler itself — is genuinely the explicit boundary an effect is supposed to live behind. Silencing
that finding requires naming the effect category, so a suppression can never accidentally cover an effect it
wasn't written for.

At one call site, on the flagged line or the line directly above it:

```fsharp
let live : IClock =
    { new IClock with
        member _.UtcNow() = DateTimeOffset.UtcNow } // axial-allow-effect: clock
```

For a whole file whose entire purpose is to be an effect's boundary (a service's `live` implementation module,
the low-level scheduler), in the file's leading comment block:

```fsharp
// This file is the live IConsole implementation: its entire purpose is to be the explicit,
// mockable boundary around System.Console, so ambient console access here is intentional.
// axial-allow-effect-file: console
```

placed directly above the file's `namespace` declaration.

`axial-allow-effect-file` only silences the categories it names — a file allowed for `console` still gets
flagged for an unrelated `System.Random()` call. There is no blanket "allow everything" form; every suppression
states which effect it covers, so a reviewer can tell at a glance whether it's the right one.

`Axial.Guardrails` itself is applied to every `src/Axial*` package and to `examples/Axial.Examples`; see
`EffectCatalog.fs` and `Suppressions.fs` in `src/Axial.Guardrails` for the exact matching and suppression rules.

## Catching stale suppressions

A suppression comment is an escape hatch, and escape hatches accumulate cruft: a category name typo'd so it
silences nothing (`// axial-allow-effect: guide` doesn't match `guid`), or a suppression left behind after the
call it once covered was refactored away or moved to a different line. Both leave a comment that looks like a
reviewed, intentional boundary but isn't.

`AXG002` (`SuppressionIntegrity`) checks for exactly that: every `axial-allow-effect`/`axial-allow-effect-file`
directive in a checked project is cross-referenced against `EffectCatalog.knownCategories` and against the actual
`AXG001` findings it's positioned to cover. `scripts/run-guardrails.sh` and the packaged `Axial.Guardrails`
MSBuild target both check `AXG001` and `AXG002` together, so a mistyped or orphaned suppression fails the build
the same way an unmarked effect call does.

## Catching `raise`/`failwith` inside `flow { }`

A `Flow<'env, 'error, 'value>` already has a typed `'error` channel for expected failures. Reaching for
`raise`/`failwith`/`failwithf`/`invalidOp`/`invalidArg`/`reraise` directly inside a `flow { }` block is the single
most common mistake when writing Axial code for the first time - it's the instinct carried over from ordinary
F#/C#, and it silently turns what should be an `'error` value into an unhandled defect (`Cause.Die`) instead,
which a caller matching on `'error` never sees coming.

`AXG003` (`RaiseInFlow`) flags exactly that: any of those calls found lexically inside a `flow { }` block,
including inside a nested `if`/`match`/`let`/lambda within it. Prefer `return! Flow.fail err` (or `Flow.die`, if
the failure genuinely is meant to be an unrecoverable defect). If the call is deliberate - most often because a
surrounding `try`/`with` inside the same block already converts every exception into a typed error, and `raise` is
just the local control-flow escape into that boundary - mark it with `// axial-allow-raise` on the flagged line or
the line above it. There's no category to name; unlike `axial-allow-effect`, this is a single, generic escape
hatch, so use it only when the raise is genuinely caught and translated nearby, not as a shortcut past the finding.
