---
title: Effect-boundary guardrails
linkTitle: Effect-boundary guardrails
description: Use Axial.Guardrails to detect ambient effects, exceptions in workflows, shared test fixtures, discarded cancellation tokens, and resources returned from fibers.
---

# Effect-boundary guardrails

## Repository policy lock

`axial check` compares the repository policy, baseline, and analyzer configuration with `Axial.Guardrails.lock`. Policy drift produces one loud warning with a change count and an instruction to run `axial policy review`, but exits zero. Corrupt locks and unreadable policy still fail; analyzer errors still fail builds. The lock is tamper-evident, not a security boundary; enforcement is the repository's choice.

Builds run the same check per project and target framework (`axial check --build`), but the build task shows the
drift warning once per drift state in a build invocation (per MSBuild node), so a solution build shows one line rather
than one per project/TFM. The next build that rechecks the policy shows it again; nothing depends on the clock or on
temporary files. It is always a warning, never an error;
`axial check --full` and release behaviour are unchanged. Run directly, `axial check` always prints the warning and
also a per-project raw-Task trend on AXG014's scale: Fast's AXG014 findings in scope (with file roles, tiers,
`[files]` overrides and `[runtime].files`), how many of them are acknowledged, and every raw `task { }` and
Task-returning signature as a secondary count, with the project's role (`--json` adds a `rawTask` array). The build
records these counts: each Fast or Full result (`axial-guardrails.<TFM>.result.xml` under `obj`) carries Fast's count
per file, and cache replays keep it. `axial check` reads each project's latest complete result and parses no source;
a project without one is listed as "not built yet — run a build" (`--json`: `"built": false`, null counts). Fixing an
AXG014 finding lowers it after the next build. It is a trend to watch, not a gate; AXG014 gives the per-site guidance. In this repository, `scripts/axial <command>` builds and runs the live tool.

Run `axial policy review [--since <ref>] [--json]` to inspect what loosened: new or changed suppression reasons and notes, relaxed roles or rule levels, runtime/boundary/ignore/baseline additions, evaluated project settings, and changes to declared automation files or analyzer versions. Removals appear briefly as tightening. The default reference is the reviewed commit recorded in the lock; the first review needs `--since <tag-or-commit>`. If that reference predates policy files, the review reports policy introduction rather than inventing historical policy, and still compares source exceptions.

`axial check --full` first enforces `axial policy review --check`, then builds the single solution (`.slnx` or `.sln`) in the current directory with explicit Full mode and checkpoint enforcement. The configuration defaults to Release; use `--configuration <name>` to override it. It analyzes every enabled F# project and target framework, including frameworks normally restricted by a designated-framework setting. Exported results must match the build's expected project/TFM set and have complete Full coverage. Infrastructure failures, missing or incomplete records, skipped sources (`AXGINF002`, which ordinary builds only warn about; the message names each file and reason), and Error findings produce a nonzero exit; Warning, Hint and baselined findings retain their non-blocking severity. Build diagnostics stream to stderr, leaving `--json` stdout parseable. The check uses MSBuild's `CustomAfterMicrosoftCommonTargets` hook to collect coverage; a value set in the environment is imported from it rather than replaced. A first policy review still needs a human-reviewed reference recorded in the lock. The command never approves policy or generates a baseline.

An F# project in the solution that does not reference `Axial.Guardrails` is not analyzed. `axial check --full` lists
each such project once, after its result, in a `WARNING: ... do not reference Axial.Guardrails` message, so a library
is not left unchecked without notice. The warning never changes the exit code. Add the package to the project, or opt
it out with `<AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>` in its project file to remove it from the list.
C# and other non-F# projects are not listed.

`AxialGuardrailsMode=Fast|Full` selects the build mode. **Fast is the default** in the package and this repository.
Fast parses changed files in-process, without the analyzer CLI or a second typecheck. It gives syntax advice, not
complete typed coverage. Use `dotnet build -p:AxialGuardrailsMode=Full` for all typed checks, or `axial check --full`
for the Full solution checkpoint and policy-review gate. CI explicitly builds in Full; release automation uses
`axial check --full`. Ordinary CI builds warn about policy drift rather than requiring approval on every push.
Values are case-sensitive: `fast`, `FULL`, and other invalid values fail. Neither `CI`, Release configuration nor
branch names select a mode. Changing modes invalidates cached analysis in either direction; Fast can never satisfy Full.

### Fast advice versus Full checks

| Rule | Fast | Full |
|---|---|---|
| AXG001 ambient effects | Catalog spellings that resolve through the file's `open`s (fully qualified, or `IO.File` after `open System`), PascalCase members of wildcard types, and `.GetAwaiter().GetResult()` chains; replacement guidance; project role from TOML, overridden by `[files]` | Resolved symbols, aliases, overloads, scope and completed-task-read exceptions |
| AXG003 raise in flow | Direct raise/failwith in syntactically identified `flow { }`; generic advice | Resolved error type and type-safe fix suggestions |
| AXG004 fixtures | Shared parse-only visitor, including attributes and nested modules | Same fixture shapes |
| AXG006 reflection formatting | Literal `%A`/`%+A` format strings at direct formatting calls | Also type-dependent interpolation, ToString and reflection targets |
| AXG009 raw concurrency | Direct qualified constructor/API candidates; shared test/capability/runtime/declared-host exemptions | Resolved identity and policy-specific typed classification |
| AXG013 guessed waits | In resolved test-role files: `Thread.Sleep` calls, and `Task.Delay`/`Async.Sleep` awaited directly by `do!`/`let!`, except infinite cancellation waits and the shared `waitUntil`/`occupyThread` helpers | Resolved symbols for the same shapes, cancellation-parameter overloads and timeout constants |
| AXG014 task surface | `task`/`backgroundTask` by name (unshadowed) and annotated return types spelling `Task`/`ValueTask` through the file's `open`s; same roles and composition rule as Full | Resolved builder type (aliases such as `let t = task`), abbreviations and inferred return types |
| Other checks, including suppression/note integrity | Explicitly deferred to Full | Every enabled analyzer |

Ambiguous syntax is labelled `[fast/unverified]` and emitted as Info, even under warnings-as-errors; it never
blocks a build. AXG004 is labelled `[fast hint; syntax-exact]` and is a Warning in Fast, baselined or not: guidance
never fails an ordinary build. Full keeps AXG004's policy severity (Error unless baselined).
Fast conservatively skips names shadowed or aliased anywhere in the file, and short names whose namespace the file
never opens; it does not resolve aliases or receiver types (`task.Result`, `task.Wait()` are Full-only). This can
miss valid calls outside the shadow's scope. Inactive `#if` branches do not produce advice. A hint can disagree
with Full: do not treat the absence of Fast hints as proof that the typed rules pass.

Effects belong in host or capability-role files; domain and service code should use explicit effect services.
File roles override project roles, so a domain project can keep all raw effects in separate capability files.
AXG001 guidance also runs in test projects: in the `test` role it is a **warning by default**, because tests
exercise live effects for setup and assertions. Set `[roles.test.rules]` `"effects.ambient"` to `deny` or `forbid`
to opt in to errors, or `allow` to silence it. Host and capability files are exempt from AXG001 in both Fast and Full.
`effects.ambient = "forbid"` remains unsuppressible, including in host/capability files. Reasoned file-level exceptions remain visible to
`axial policy review`; Fast does not approve them or check suppression integrity.

On .NET MSBuild, a collectible, isolated load context owns FCS, FSharp.Core and the analyzer SDK; they do not
bind to FSharp.Build's copies. Each loaded task version retains at most one such context, replacing it when its tool
identity changes. Policy/configuration is passed explicitly per request, not through process environment variables. Per-file results are content-keyed on disk. Framework MSBuild (Visual Studio)
cannot load the net8 parser safely: requesting Fast there explicitly falls back to out-of-process Full and says so.
Outside checkpoints that fallback reports no more severely than Fast: Error findings (for example AXG001 or AXG004
at `deny`) become warnings, so a Visual Studio build fails on guidance no more than `dotnet build` does. The targets
pass this to the analyzers explicitly (`severityCap` in the rules file); with `AxialGuardrailsCheckpoint=true` the
fallback keeps policy severity. Use `dotnet build` for Fast feedback. When sources are newer than the assembly, a background warm-up loads the isolated parser and parses
the newest source while CoreCompile runs; it writes nothing and cannot change the outcome. Without it, a one-file
edit in a fresh build process paid about a second of FCS JIT time. Parsing uses the current `DefineConstants`, `LangVersion`, `OutputType`
and direct `OtherFlags`, even when CoreCompile reuses an old compiler-argument snapshot. Response files in
`OtherFlags` are not supported in Fast: they fail with `AXGINF001`, rather than allowing untracked defines into a
successful cache entry. Supply parsing flags directly or explicitly select Full.

`axial policy review --check` prints the same review and exits nonzero if anything loosened. Repositories may run it at release or merge time. After reading the review, a human runs `axial policy approve` (optionally `--yes-i-reviewed`) and commits the lock. Approval means a human read the review and records the current commit as the reviewed point; it has no signing, identity, or TTY requirement.

A migration prepares the TOML and reasoned exceptions, runs `axial check` and `axial policy review` (use `--since` for
the first review), and reports any proposed loosening. An agent proposes; a human reads the review, runs
`axial policy approve`, and commits the lock. There is no approval alias or `--yes-i-reviewed` recipe that lets an agent
stand in for that review. When policy files do not exist yet, `axial init` creates only the missing TOML, baseline, and
bootstrap lock; it never overwrites an existing artifact and does not approve later policy changes. Daily drift warns,
analyzer errors remain errors, and release automation runs `axial check --full`.

For a new policy, `axial init` asks which CI host the repository uses: GitHub, GitLab, Bitbucket, or Other.
Use `axial init --ci github|gitlab|bitbucket|other` to select without a prompt. With redirected stdin (or
`--json`) and no flag, init selects Other and reports that choice. Existing TOML is preserved.
Generated policies include comments explaining roles, the baseline, and automation tracking.

Declare the files that enforce policy in the repository's TOML:

```toml
[review]
automation = [".github/workflows/**", "scripts/verify.sh"]
```

Only files matching `[review].automation` are inventoried as automation by `axial policy review`; an absent or
empty list means no automation tracking. The first review after upgrading lists the declared automation files
as added. Adding a verification script to the list makes its content changes,
addition, and deletion review items; it does not run the script. Paths are repository-relative and use the same
`*`, `**`, and `?` globs as other policy lists. Patterns may match no files. Unknown `[review]` keys are rejected.
Init selects `.github/workflows/**` for GitHub, `.gitlab-ci.yml` and `.gitlab/ci/**` for GitLab,
`bitbucket-pipelines.yml` for Bitbucket, and `[]` for Other. Add your verification scripts explicitly.

Relative compiler source paths resolve against the compiler project's directory for both policy classification and
baseline identity, not the analyzer process's working directory. Policy inventory rejects baseline symlinks outside
the repository, including the default baseline when TOML omits `baseline`. `axial init` checks config containment
before reading it, even when a lock already exists.

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

Do not set Guardrails policy through MSBuild severity, boundary, baseline-path, or test-policy settings.
`AxialGuardrailsSeverity` (MSBuild property or environment variable) and `AXIAL_GUARDRAILS_SEVERITY` are removed:
`AXIALGR001` fails the build or standalone analyzer/CLI invocation, even when analysis is disabled. Remove the
setting and configure supported per-role rule levels in `Axial.Guardrails.toml`, for example
`[roles.domain.rules]` with `"effects.ambient" = "deny"`. AXG009–013 retain their fixed block/guide policy;
`AxialGuardrailsCheckpoint` controls unacknowledged guides at checkpoints. For local
troubleshooting, pass `-p:AxialGuardrailsEnabled=false` on the command line rather than storing it in shared build files.
Measurement projects explicitly set `<AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>` in their project file;
this exclusion appears in `axial policy review` as a loosened evaluated setting.
If a project cannot be evaluated, review lists `cannot evaluate <project>: <reason>` and continues with the other
projects; `--check` fails until that review item is resolved.

For adoption guidance, see [Installation](/getting-started/installation.html#add-effect-boundary-guardrails-optional).

### Build reports and timings

Each project/target framework writes `axial-guardrails.<TFM>.result.xml` in its intermediate output directory
(usually under `obj`). The report contains findings and a rule/file coverage matrix. `ran` records that the
entry point completed, including when policy makes it return no findings; `skipped` includes a failure reason.
In Fast reports, `deferred to full` identifies typed-only rules; it is intentional Fast coverage, never Full coverage.
`excluded (generated)` marks a compile item under the project's intermediate output directory
(`$(BaseIntermediateOutputPath)` or `$(IntermediateOutputPath)`, compared by full path), such as the SDK's generated
`AssemblyInfo.fs` or xunit.v3's `XunitAutoGeneratedEntryPoint.fs`. No rule checks it and it reports no finding,
because the consumer cannot edit generated code; it counts as complete coverage in Fast, Full and `axial check --full`,
and is never an `AXGINF002` skip. Keep hand-written sources outside `obj`.
Fast per-file `cacheHit` attributes show which files were reparsed. Parse errors and parse/visitor exceptions fail
closed, retain other available findings and never stamp incomplete coverage.
Only complete coverage for the current input fingerprint can produce a reusable stamp. Cache hits replay findings:
errors and warnings at their severity, and Info at normal verbosity, so a minimal-verbosity console shows an Info
finding only when analysis produced it. Unreadable, truncated, or mismatched reports rerun analysis.
A build that builds one project/TFM more than once, for example through references with different
`AdditionalProperties`, shows that result's warnings and Info once per build node; errors are reported by every
instance. MSBuild's console summary at the end of a build lists each warning a second time.
A source the analyzers cannot classify (an unusable source or project path) is recorded as a `skip` in the report and
shown as warning `AXGINF002` on every build, cache replays and reused build nodes included; `axial check --full`
rejects it. With an unusable
source path no rule checks the file; with an unusable project path the file is analyzed under the default policy
instead of its project's role. Fast's per-file cache
under `axial-guardrails.<TFM>.result.xml.files` keeps only the entries of the current request.

Analyzer exceptions leave other rules running; process failures and incomplete coverage leave no complete stamp.
`AXGINF001` fails the build with one MSBuild error per project/target framework, naming the cause, skipped-check
count and up to three rule/file examples. There is no opt-out: a successful compilation alone does not establish
complete analysis. Available findings are still reported at their policy severity, including Error findings replayed
from cache. Candidate-baseline generation also rejects incomplete coverage rather than accepting missing findings.

Set `-p:AxialGuardrailsTimings=true` to write `axial-guardrails.<TFM>.timings.xml` beside the report. It records
fingerprinting, CLI startup/analyzer loading, compiler-argument or project loading, FCS checking, each rule/file,
and output time in milliseconds. CLI phase boundaries are observed from its diagnostic log; parallel rule times
overlap and must not be summed as wall time. Timing collection is off by default.

Source and configuration contents are rehashed on every request. Binary digest reuse on supported 64-bit Linux
local filesystems (ext, XFS, Btrfs, tmpfs and overlay) verifies device, inode, size and kernel-managed nanosecond
change time, not just a restorable modification timestamp. A changed identity triggers content hashing; other
platforms/filesystems rehash. This assumes ordinary filesystem writers, not privileged manipulation of filesystem
metadata. Tool, ordered compiler arguments, policy, source order and target-framework inputs all participate in the
analysis fingerprint. Full additionally fingerprints references and its analyzer CLI; Fast does not consume either.
Protected-input discovery skips `.git`, `.livedocs`, legacy `.fsdocs`, `node_modules`, `artifacts`, `bin`, `obj`, and
`output`. Explicit MSBuild imports remain inputs even under these directories. On supported local Linux filesystems,
a content-validated directory-inventory snapshot checks kernel change times to discover added/removed files without
recursively enumerating unchanged trees; protected file contents are still rehashed. Other platforms enumerate.

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
The Axial guardrail asks for an explicit, mockable alternative: use Axial.IClock (Clock.now).
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

This preparation command is safe for an agent: it builds repository F# projects using their Guardrails imports,
collects current findings for the selected diagnostic, and merges candidate entries into the TOML baseline path.
`effects.ambient` selects AXG001; diagnostic IDs AXG001–008 are also accepted. Existing entries are retained;
repeated preparation and shared sources across target frameworks do not multiply counts. Fingerprints use
repository-relative source paths, even when the baseline lives in a subdirectory.

The lock and its reviewed commit are never changed, and preparation does not prompt for approval. Only the selected
legacy diagnostic is temporarily reported as a warning while collecting candidates. Resolve compilation failures,
tool errors, and unrelated error diagnostics before preparing; a failed build leaves the baseline unchanged.
This requires the 0.12 Guardrails build imports with analysis enabled.

A human reviews every candidate entry rather than hand-writing fingerprints, runs `axial policy review`, and then
records approval separately:

```bash
axial policy approve --baseline-rule effects.ambient
```

`axial check` warns about baseline growth against the reviewed lock during local and CI builds. Ordinary
`axial policy approve` refuses growth. The human-only `--baseline-rule <id>` option permits it only when the selected
rule was newly enabled for every affected file's effective role. Preparing candidates does not bypass that check.

### Migrate file-level effect allowances

`axial-allow-effect-file` is not recognized in 0.12. If the file implements a primitive effect, assign it the
`capabilities` role in `[files]` or `[projects]`. For an exceptional boundary file instead, list its exact
repository-relative path in `[boundaries].files` and use a rule-specific allowance, for example
`// axial-allow-file-AXG001: live system clock implementation` in the leading ordinary-comment block.
The boundary declaration alone does not suppress findings. A `forbid` level cannot be suppressed.
For legacy domain code, prepare a baseline and have a human review and approve it as described above.
AXG009–013 do not accept file allowances or baseline downgrades.

## Declare host project exemptions

`[exemptions]` in `Axial.Guardrails.toml` maps `AXG009`, `AXG010`, `AXG011`, or `AXG012`
to arrays of repository-relative project-path globs. Only matching `host` projects are exempt;
other roles and other diagnostics are unchanged. No tooling or benchmark directory is implicitly exempt.
Unknown keys, non-array values, escaping paths, and patterns matching no files are rejected.
See the [diagnostic reference](/guardrails/diagnostics.html) for an example.

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
| `AXG010` | `SwallowedException` | Catch-all handlers ignoring the exception; narrow to specific exception types (guide) |
| `AXG011` | `UncancellableWork` | `CancellationToken.None` outside `Cancellation.never`, the one definition that owns it |
| `AXG012` | `DiscardedWork` | Typed Task/ValueTask/Fiber discards |
| `AXG013` | `WaitForFacts` | Blocking sleeps (error) and directly awaited delays (guide) in test-role code |
| `AXG014` | `TaskSurface` | Raw `task { }` and Task/ValueTask-returning public signatures in domain/service code; composing task bridges elsewhere (guide, always Info) |
| `AXG007` | `ResourceFromFiber` | A forked flow that returns a scoped resource it acquired, which the fiber releases before `Fiber.join` returns it |
| `AXG008` | `ScopeFromRun` | A run returning a scope-owned `Queue.makeScoped`, `Hub.makeScoped` or `Hub.subscribe` result, or a cache tied to its creating runtime |

`AXG003`, `AXG005`, `AXG006`, `AXG007`, `AXG008`, and `AXG014` run outside the declared `test` project role.
Test-role projects select EffectBoundary, Fixture, WaitForFacts, RawConcurrency, and SuppressionIntegrity by default.
AXG001 follows the resolved file role; isolate raw setup effects in host/capability-role files or explicitly allow
them in policy. Exceptions remain available for setup and assertions. Declare `[projects]` with
`test = ["tests/**/*.fsproj"]` (or use `test` as the default role). This selection does not depend on
`IsTestProject` or Microsoft.NET.Test.Sdk; file-role overrides do not change the project-wide analyzer set.
Analyzers resolve the project role in-process; no policy-tool process is launched to select the set.
Set `AxialGuardrailsInTests` to `all` to run every enabled analyzer in a test-role project too. Policy review
compares this evaluated property: enabling `all` is a tightening, and removing `all` is a loosening.
Non-test analyzers return before walking symbols or typed trees when the reduced test set is selected.
Both this setting and the TOML policy
invalidate cached analysis when changed.

`AXG001`, `AXG002`, `RawConcurrency`, and `WaitForFacts` also run in test projects by default. Raw task bridges remain
exempt in the test role unless `AxialGuardrailsInTests=all`. `lock`, `Monitor` and `Interlocked` are not reported anywhere.

For AXG010, when the exception value is irrelevant, catch the specific type(s), for example
`| :? FileNotFoundException ->`; a typed catch need not use the value. Do not force the exception into a return
value to silence the guide. Host boundary projects can declare an `AXG010` exemption as described above.

In Full mode, guide findings (AXG010 and awaited-delay AXG013) warn in ordinary builds. A valid
`// axial-note AXGnnn: <reason>` on the finding's line or the preceding line changes a guide finding to visible Info.
Invalid notes warn under AXG002. Verification and release builds set `AxialGuardrailsCheckpoint=true` to reject
unacknowledged guides and invalid notes; noted findings remain Info. Ordinary Release builds are not checkpoints.
Block findings do not accept notes or baseline downgrades. AXG001–008 retain their existing allowance/baseline behavior.
Checkpoints, including `axial check --full`, reuse complete Full results only for identical fingerprinted inputs and
checkpoint settings. Cache hits replay findings and export the same project/TFM coverage; a Fast result cannot satisfy Full.

`AXG004` checks shared fixtures in modules containing xUnit tests. It is included in the test-role analyzer set
and in the full analyzer set; modules without tests do not produce fixture findings.

## Choose guardrails per package

The rules are grouped into guardrails, each named after the package whose service replaces the effect it flags.
Referencing a package turns its guardrail on: a rule that recommends `IHttp` is only useful once `IHttp` is available.

| Guardrail | Rules |
| --- | --- |
| `Axial` | Ambient clock reads, `Stopwatch`, `Task.Delay`, `Thread.Sleep`, `Environment.ProcessorCount`, ordinary and cryptographic randomness, GUIDs, `TimeZoneInfo.Local`, blocking waits, and `AXG002`–`AXG014` |
| `Axial.Configuration` | Ambient environment state |
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
[rule-authoring reference](/guardrails/rule-authoring.html) documents the `AxialGuardrailRule` metadata and the `Match`
grammar.

## Diagnostic reference

Each diagnostic's contract — what it detects, what to use instead, and whether it permits a boundary allowance
or a visible guide acknowledgement — is on the [diagnostics reference](/guardrails/diagnostics.html):

- [`AXG001`](/guardrails/diagnostics.html#axg001-use-explicit-effect-services): ambient .NET effects.
- [`AXG002`](/guardrails/diagnostics.html#axg002-remove-invalid-suppressions): invalid or unused suppressions.
- [`AXG003`](/guardrails/diagnostics.html#axg003-return-typed-failures-from-workflows): exceptions raised in `flow { }`.
- [`AXG004`](/guardrails/diagnostics.html#axg004-create-fresh-test-fixtures): shared module-level test fixtures.
- [`AXG005`](/guardrails/diagnostics.html#axg005-preserve-cancellation-tokens): task adapters that drop their token.
- [`AXG006`](/guardrails/diagnostics.html#axg006-format-without-reflection): reflection-dependent formatting.
- [`AXG007`](/guardrails/diagnostics.html#axg007-keep-resources-acquired-by-a-fiber): resources returned from fibers.
- [`AXG013`](/guardrails/diagnostics.html#axg013-wait-for-facts-not-time): blocking test sleeps (error) and awaited delays (guide). Runs only in policy role `test`; legacy `test.wait-for-facts` levels do not change the tiers. Workflow clock sleeps are out of scope.
- [`AXG008`](/guardrails/diagnostics.html#axg008-keep-scope-owned-services-inside-their-run): scoped queues and hubs, subscriptions, and caches returned by a run. Unscoped `Queue.make` (including its shorthands) and `Hub.make` are allowed.
- [`AXG014`](/guardrails/diagnostics.html#axg014-keep-raw-task-at-the-edge): raw Task in domain/service code and composing task bridges (guide, never blocks).
