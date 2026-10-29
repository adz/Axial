---
title: Effect-boundary guardrails
linkTitle: Effect-boundary guardrails
description: Use Axial.Guardrails to detect ambient effects, exceptions in workflows, shared test fixtures, discarded cancellation tokens, and resources returned from fibers.
---

# Effect-boundary guardrails

## Repository policy lock

`axial check` compares the repository policy, baseline, and analyzer configuration with `Axial.Guardrails.lock`. Policy drift produces one loud warning with a change count and an instruction to run `axial policy review`, but exits zero. Corrupt locks and unreadable policy still fail; analyzer errors still fail builds. The lock is tamper-evident, not a security boundary; enforcement is the repository's choice.

Run `axial policy review [--since <ref>] [--json]` to inspect what loosened: new or changed suppression reasons and notes, relaxed roles or rule levels, runtime/boundary/ignore/baseline additions, evaluated project settings, and changes to verification scripts, workflows, or analyzer versions. Removals appear briefly as tightening. The default reference is the reviewed commit recorded in the lock; the first review needs `--since <tag-or-commit>`. If that reference predates policy files, the review reports policy introduction rather than inventing historical policy, and still compares source exceptions.

`axial policy review --check` prints the same review and exits nonzero if anything loosened. Repositories may run it at release or merge time. After reading the review, a human runs `axial policy approve` (optionally `--yes-i-reviewed`) and commits the lock. Approval means a human read the review and records the current commit as the reviewed point; it has no signing, identity, or TTY requirement.

Mandatory protected inputs include the TOML policy, baseline, `.editorconfig`/`.globalconfig`, `Directory.Build.*`, and all `.props`/`.targets` files. Guardrails test sources and project files are not automatically protected; adding, editing, or deleting them does not require approval unless the policy explicitly protects them. The lock itself records the approved snapshot.

Axial models operational effects as explicit, mockable dependencies. Direct access to ambient state can bypass those dependencies without changing a function's signature.

`Schedule.jittered` once created its own `System.Random`, and `FiberDump` once read `DateTimeOffset.UtcNow` directly. Both compiled because the build did not enforce the effect boundary.

`Axial.Guardrails` is an [FSharp.Analyzers.SDK](https://ionide.github.io/FSharp.Analyzers.SDK/) analyzer package. It checks effect boundaries and related Axial conventions during each build.

## Install the analyzer

Add the package to your project:

```bash
dotnet add package Axial.Guardrails
```

The package configures the analyzer and runs `axial check` before analysis. Put `Axial.Guardrails.toml`,
`Axial.Guardrails.lock`, and the configured baseline at the repository root. Which rules run also depends on the Axial
packages the project references; see [Choose guardrails per package](#choose-guardrails-per-package).

A minimal policy defaults source to the `domain` role and declares capability implementations explicitly:

```toml
version = 2
baseline = "Axial.Guardrails.baseline"

[default]
role = "domain"

[files]
capabilities = ["src/App/Capabilities/**"]
services = ["src/App/Services/**"]

[roles.domain.rules]
"effects.ambient" = "deny"

[roles.capabilities.rules]
"effects.ambient" = "allow"
```

The five roles are `capabilities`, `services`, `domain`, `host`, and `test`. File assignments override project
assignments, which override the default. Rule levels are `allow`, `warn`, `deny`, and `forbid`; `forbid` cannot be
suppressed. Suppressions remain active while policy review is pending. Policy changes warn during ordinary builds;
`axial policy review` groups the changes for later human review. Removing suppressions or shrinking baseline entries
tightens policy; the next approval refreshes the snapshot.

Use `[suppressions]` with `ignore = ["tests/AnalyzerFixtures/**"]` to exclude fixture sources from the policy
lock's suppression scan. Without this optional setting, every repository `.fs` file is scanned; no project paths
are excluded automatically. Patterns use the same repository-relative glob syntax as project and boundary paths:
`*` matches within a path segment, `**` crosses directories, and `?` matches one non-separator character.
Ignore patterns may match no files. This setting affects only lock scanning, not analyzer execution. The TOML is
tracked, so adding ignore entries appears in the next policy review.

Lock format version 2 records each suppression by repository-relative file, normalized comment text, and occurrence
number, not source line. Normalization selects the `// axial-allow-AXGnnn:` comment, canonicalizes spacing before the
rule, and trims the reason's outer whitespace; internal reason text remains significant. Moving a comment within
its file does not add a review item. Adding a duplicate, changing its rule or reason, or moving it to another file appears in the review.
`SUPPRESSION` records contain tab-separated file, occurrence number, and comment text. `BASELINE_PATH` records the
configured baseline path. Version-1 locks remain readable but always require one reviewed `axial policy approve`
to migrate; they are never silently trusted.

Do not set Guardrails policy through MSBuild severity, boundary, baseline-path, or test-policy settings. For local
troubleshooting, pass `-p:AxialGuardrailsEnabled=false` on the command line rather than storing it in shared build files.
Measurement projects explicitly set `<AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>` in their project file;
this exclusion appears in `axial policy review` as a loosened evaluated setting.
If a project cannot be evaluated, review lists `cannot evaluate <project>: <reason>` and continues with the other
projects; `--check` fails until that review item is resolved.

For adoption guidance, see [Installation](/getting-started/installation.html#add-effect-boundary-guardrails-optional).

### Verify the analyzer runs

A minimal deliberate finding confirms that the package actually runs and fails at error severity. In a consumer
project that references `Axial.Guardrails` and `Axial`, put this in a source file:

```fsharp no-check reason="Deliberate finding in a separate consumer project, not part of the docs build"
module Consumer
let now () = System.DateTimeOffset.UtcNow
```

Build the project. The build exits non-zero and reports the finding:

```text
error AXG001: This call reads the ambient system clock through System.DateTimeOffset, which makes timing untestable.
The Axial guardrail asks for an explicit, mockable alternative: use Axial.IClock (Axial.PlatformService.Clock.now).
```

If you see no finding, the analyzer is not running in that project: check the package reference and that the
guardrail for the effect's package is enabled (the core `Axial` guardrail covers clock reads).

## Adopt Guardrails with a baseline

For AXG001–008, a baseline records violations that already exist when Guardrails is introduced. It uses tab-separated entries:

```text
AXG001<TAB>src/Legacy.fs<TAB>6ea89f…
AXG001<TAB>src/Duplicates.fs<TAB>218f34… x2
```

The optional trailing count represents multiple diagnostics with the same fingerprint. It is omitted for one and must
be at least two when present; duplicate lines are not canonical. The fingerprint contains the rule ID, normalized
offending text, and enclosing declaration signature. Moving a finding to another line, inserting matching text in a
comment or string, or removing an earlier duplicate does not invalidate it. Changing the offending expression or
moving it to another declaration does. If diagnostics exceed the recorded count, the later diagnostics are errors and
report the baseline and actual counts.

Generate a candidate for one newly introduced rule:

```bash
axial policy baseline --rule effects.ambient
```

Review every entry written to the baseline path declared in TOML. Regenerate and review rather than hand-writing
fingerprints, then approve the resulting policy state with the applicable `axial` workflow.

`axial check` warns about baseline growth against the reviewed lock during local and CI builds. To adopt a newly enabled
rule with existing findings, run `axial policy baseline --rule <id>`, review the candidate entries, and approve the
policy change. Ordinary policy approval refuses baseline growth.

## Declare host project exemptions

`[exemptions]` in `Axial.Guardrails.toml` maps `AXG009`, `AXG010`, `AXG011`, or `AXG012`
to arrays of repository-relative project-path globs. Only matching `host` projects are exempt;
other roles and other diagnostics are unchanged. No tooling or benchmark directory is implicitly exempt.
Unknown keys, non-array values, escaping paths, and patterns matching no files are rejected.
See the [diagnostic reference](04-guardrail-diagnostics.html) for an example.

## Review the diagnostics

| Code | Name | Detects |
| --- | --- | --- |
| `AXG001` | `EffectBoundary` | Direct calls to ambient .NET effects |
| `AXG002` | `SuppressionIntegrity` | Invalid or unused effect suppressions and guide notes |
| `AXG003` | `RaiseInFlow` | Direct exception-raising calls inside `flow { }` |
| `AXG004` | `Fixture` | Shared module-level values in xUnit test modules |
| `AXG005` | `DiscardedCancellation` | Task adapters that discard their cancellation token |
| `AXG006` | `ReflectionFormatting` | Formatting that needs F# reflection, which NativeAOT and trimming remove |
| `AXG009` | `RawConcurrency` | Raw task bridges (`TaskCompletionSource`, `Task.Run`, `ContinueWith`, threads, `CancellationTokenSource`) outside their allowed roles/files |
| `AXG010` | `SwallowedException` | Catch-all handlers ignoring the exception (guide) |
| `AXG011` | `UncancellableWork` | `CancellationToken.None` outside `Cancellation.never`, the one definition that owns it |
| `AXG012` | `DiscardedWork` | Typed Task/ValueTask/Fiber discards |
| `AXG013` | `WaitForFacts` | Blocking sleeps (error) and directly awaited delays (guide) in test-role code |
| `AXG007` | `ResourceFromFiber` | A forked flow that returns a scoped resource it acquired, which the fiber releases before `Fiber.join` returns it |
| `AXG008` | `ScopeFromRun` | A run returning a scope-owned `Queue.makeScoped`, `Hub.makeScoped` or `Hub.subscribe` result, or a cache tied to its creating runtime |

`AXG001`, `AXG003`, `AXG005`, `AXG006`, `AXG007`, and `AXG008` run in application and library projects. They don't run in test projects, where direct effects and exceptions are often necessary for setup and assertions. Set `AxialGuardrailsInTests` to `all` to run them in a test project too.

`AXG002`, `RawConcurrency`, and `WaitForFacts` also run in test projects by default. Raw task bridges remain
exempt in the test role. `lock`, `Monitor` and `Interlocked` are not reported anywhere.

Guide findings (AXG010 and awaited-delay AXG013) warn in ordinary builds. A valid
`// axial-note AXGnnn: <reason>` on the finding's line or the preceding line changes a guide finding to visible Info.
Invalid notes warn under AXG002. Verification and release builds set `AxialGuardrailsCheckpoint=true` to reject
unacknowledged guides and invalid notes; noted findings remain Info. Ordinary Release builds are not checkpoints.
Block findings do not accept notes or baseline downgrades. AXG001–008 retain their existing allowance/baseline behavior.

`AXG004` runs only in projects where MSBuild sets `IsTestProject`. It checks a test-specific risk and does not apply to application or library projects.

## Choose guardrails per package

The rules are grouped into guardrails, each named after the package whose service replaces the effect it flags.
Referencing a package turns its guardrail on: a rule that recommends `IHttp` is only useful once `IHttp` is available.

| Guardrail | Rules |
| --- | --- |
| `Axial` | Ambient clock reads, `Stopwatch`, `Task.Delay`, `Thread.Sleep`, `Environment.ProcessorCount`, blocking waits, and `AXG002`–`AXG013` |
| `Axial.PlatformService` | Ordinary randomness (`random`), cryptographic randomness (`secure-random`), GUIDs, environment state, and `TimeZoneInfo.Local` |
| `Axial.Console` | `System.Console` |
| `Axial.FileSystem` | `System.IO.File` and `System.IO.Directory` |
| `Axial.Process` | `Process.Start` |
| `Axial.HttpClient` | `new HttpClient` |

Change the set with ordinary MSBuild item operations in a project or `Directory.Build.props`. Removing an item is
an explicit choice not to run that package's rule set; it is not a severity downgrade for findings from enabled
rules:

```xml
<ItemGroup>
  <!-- Turn a guardrail off -->
  <AxialGuardrail Remove="Axial.Console" />
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

Each diagnostic's contract — what it detects, what to use instead, and whether it permits a boundary allowance
or a visible guide acknowledgement — is on the [diagnostics reference](guardrail-diagnostics.html):

- [`AXG001`](guardrail-diagnostics.html#axg001-use-explicit-effect-services): ambient .NET effects.
- [`AXG002`](guardrail-diagnostics.html#axg002-remove-invalid-suppressions): invalid or unused suppressions.
- [`AXG003`](guardrail-diagnostics.html#axg003-return-typed-failures-from-workflows): exceptions raised in `flow { }`.
- [`AXG004`](guardrail-diagnostics.html#axg004-create-fresh-test-fixtures): shared module-level test fixtures.
- [`AXG005`](guardrail-diagnostics.html#axg005-preserve-cancellation-tokens): task adapters that drop their token.
- [`AXG006`](guardrail-diagnostics.html#axg006-format-without-reflection): reflection-dependent formatting.
- [`AXG007`](guardrail-diagnostics.html#axg007-keep-resources-acquired-by-a-fiber): resources returned from fibers.
- [`AXG013`](guardrail-diagnostics.html#axg013-wait-for-facts-not-time): blocking test sleeps (error) and awaited delays (guide). Runs only in policy role `test`; legacy `test.wait-for-facts` levels do not change the tiers. Workflow clock sleeps are out of scope.
- [`AXG008`](guardrail-diagnostics.html#axg008-keep-scope-owned-services-inside-their-run): scoped queues and hubs, subscriptions, and caches returned by a run. Unscoped `Queue.make` (including its shorthands) and `Hub.make` are allowed.
