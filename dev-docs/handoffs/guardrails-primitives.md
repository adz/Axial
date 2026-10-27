# Guardrails primitives handover — Phase 1

## State and constraints

- Completed inventory/design only; STOP before Phase 2.
- Branch/worktree: `feature/flow-uninterruptible`, `/home/adam/projects/Axial/feature/flow-uninterruptible`.
- Starting HEAD and origin/main: `f76d8476ccf800a00710a34422fde79e0a10ce43`.
- Initial worktree clean. No source/test changes. No dotnet, builds, tests, benchmarks, agent runs, or verification executed: a quiet-machine benchmark is running.
- Full report: `/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives.report.md`.
- Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives.md`.
- First read `/home/adam/projects/scratch/axial-coord/pi/TOKEN-RULES.md` and the full design `/home/adam/projects/Axial/main/dev-docs/current-ideas/guardrails-exceptionless.md`.
- Commit, no push. Never modify guardrail TOML/lock/baseline, scripts/verify.sh, or analyzers.

## Proposed design

In the existing internal `Axial.Platform` module (Platform.fs compiles before Core.fs):

```fsharp
module Cancellation =
    let never : CancellationToken = CancellationToken.None

module Report =
    let inline safely (notify: unit -> unit) : unit =
        try notify () with _ -> ()
```

Use `Platform.Cancellation.never` at all existing token sites without changing invocation/runtime/ownership semantics. This names a non-cancelable token, covering cleanup, roots, scheduling, and lifetime waits; it does not pretend to implement whole-region masking. Keep releaseFinalizer's existing BranchToken/OuterToken masking intact. Update XML references to prose so exactly one literal remains in src.

**Public Flow.uninterruptible deferred.** Invocation-token substitution alone does not settle ambient fork-token masking, structured scope ownership, pending cancellation on entry/exit, or ownership of a value/resource produced inside a masked region. Existing ensuring/acquireRelease/onExit already promise uninterrupted cleanup. The lane brief permits the internal-only choice; do not add an underspecified public API just to name token substitutions.

`Report.safely` replaces existing synchronous best-effort reporting catches, each composed sink independently. Preserve silent sink-failure behavior: recursively reporting sink failures risks recursion; a new counter adds state/hot-path work and changes observability. Inline proposed to avoid new thunk allocations; benchmarks must prove no B/op increase.

Add `InternalsVisibleTo("Axial.ElmishFlows")` for Diagnostics.fs helper reuse; Process already has friend access. Do not expose these helpers publicly.

### Behavior conflict — do not silently fix

`src/Axial/Runtime.fs:65` directly invokes `current.TelemetrySink item`: a sink installed by Telemetry.withSink can currently fault the workflow. Composed telemetry sinks are already safe. Guarding the direct call changes a previously surfaced defect into success, forbidden by TOKEN-RULES without explicit approval. Preserve it and report the limitation, or obtain coordinator/owner approval for an explicit behavior change with tests/release note. ProcessLive callbacks intentionally propagate ObserverDefectException too; those are operational callbacks, not diagnostic sinks.

## Complete token inventory (original line numbers)

33 matching lines: 30 code uses, 3 XML references. Paths below begin under `src/`.

| File | Lines | Category |
|---|---|---|
| Axial/Platform.fs | 1593 | Exit-observation continuation scheduling |
| Axial/Flow.fs | 236 | Release after registration loses to closed scope |
| Axial/Flow.fs | 251 | Fable release invocation |
| Axial/Flow.fs | 267,268 | .NET release runtime BranchToken/OuterToken |
| Axial/Flow.fs | 273 | .NET release invocation |
| Axial/Flow.fs | 287,296 | .NET release scope close |
| Axial/Flow.fs | 315,316 | Fable release runtime BranchToken/OuterToken |
| Axial/Flow.fs | 338 | Fable release scope close |
| Axial/Flow.fs | 590,636 | Task.Run/StartNew scheduling; delegate returns Exit rather than canceled Task |
| Axial/Flow.fs | 1517 | Graceful fiber parent-token detachment |
| Axial/Flow.fs | 1574,1612 | Graceful stop request, Fable/.NET |
| Axial/Flow.fs | 1883 | Shared publisher follows owner scope, not waiting caller |
| Axial/Flow.fs | 1966 | Replacement handover ordering despite cancellation |
| Axial/Flow.fs | 2168 | Race loser scope close |
| Axial/Flow.fs | 2558 | onExit handler |
| Axial/Core.fs | 1074,1078 | Scope async/sync disposal |
| Axial/Core.fs | 1131,1132 | Default/root runtime tokens |
| Axial/Core.fs | 1382,1391 | Default optional root execution token |
| Axial/Core.fs | 1378,1387,1397 | XML references, not code |
| Axial/Concurrency.fs | 312 | Deferred.toTask lifetime wait |
| Axial/App.fs | 236 | Stop/cleanup before caller cancellation continuation |
| Axial/App.fs | 242 | Independent publication observation |
| Axial.Process/ProcessRun.fs | 491 | Stop-ladder coordinator outlives caller cancellation |

## Complete diagnostic-sink catch inventory

Paths begin under `src/`; numbers identify catch sites.

| File | Lines | Category |
|---|---|---|
| Axial/Core.fs | 421,422 | Composed OnStart |
| Axial/Core.fs | 425,426 | Composed OnEnd |
| Axial/Core.fs | 429,430 | Composed unobserved-defect sinks |
| Axial/Core.fs | 434,437,444 | Observer notifyStart/notifyEnd/notifyUnobservedDefect |
| Axial/Core.fs | 705 | GC finalizer reporting boundary |
| Axial/Core.fs | 1175,1176 | Composed annotation sinks |
| Axial/Core.fs | 1190,1191 | Composed telemetry sinks |
| Axial/Flow.fs | 1023 | Annotation emission |
| Axial/Flow.fs | 1543 | Registry failure text |
| Axial/Platform.fs | 1074 | Timeout discarded-exit notification AND task-await catch |
| Axial.ElmishFlows/Diagnostics.fs | 8 | OnDefect sink |

Important exclusions:
- Platform.fs:1074 must retain protection against faulted awaited tasks in addition to safe callback invocation.
- Platform.fs:1325 converts faulted race-loser task into onLoserSettled None; not a diagnostic swallow.
- Flow.fs:1643 is scope registration close-race recovery; Flow.fs:515 unlinks then rethrows.
- Outcome.fs:43 and ElmishFlows/Diagnostics.fs:13,19 return rendering fallbacks, not sink notifications.
- ElmishFlows/Sub.fs:24,25,46 and Cmd.fs:31,32 catch delivery failures and report them; preserve that reporting.
- Process cleanup, Files temp deletion, HTTP decoding, and analyzer recovery catches are not sink sites.
- Platform.observeExitTask callbacks can be state transitions; do not indiscriminately suppress them.

## Random

PlatformService targets `netstandard2.1;net8.0` (fsproj:4). Its Platform.fs:14–35 has FIVE locked methods. Use three branches:
1. Fable: existing Random()/byte-loop unchanged.
2. NETSTANDARD2_1: existing Random()/gate/five locks unchanged.
3. net8: System.Random.Shared, direct forwarding, no Random() construction, gate, or locks.

## Task/Deferred interop inventory and recommendation

All TCS declarations under src at inventory time:
- Axial/Platform.fs:1615,1623: existing Signal alias/factory; 1690: cancellation-aware signal wait; 1894: Async startup bridge.
- Axial/Concurrency.fs:294: existing public Deferred.toTask conversion (retain typed failure/defect/interruption mapping).
- Axial/App.fs:81: settled signal; 190: publication/failure race.
- Axial.Process/ProcessLive.fs:180: Async callback bridge with ObserverDefectException mapping.
- Axial.Process/ProcessRun.fs:239: interruptible stdin wait.
- Axial.Hosting/Hosting.fs:164: host shutdown timeout wait without canceling cleanup.
- Axial.ElmishFlows/TestRuntime.fs:88: provisioning publication/error.
- Axial.ElmishFlows/Sub.fs:17,18: startup/completion handoffs.

Propose only (do not implement unless subsequently shown small/obviously correct):

```fsharp
val taskOfAsyncWithError : CancellationToken -> (exn -> exn) -> Async<'a> -> Task<'a>
val waitTaskOrCancellation : CancellationToken -> Task -> Task<bool>
```

First classifies owned cancellation before mapping exceptions (ProcessLive). Second waits without canceling the operation, returning true for settlement and false for cancellation of the wait (Process stdin/Hosting; retain site-specific policy/logging). Specify completion/cancellation tie priority and registration disposal before implementing. Elmish handoffs should consider existing Platform.Signal rather than invent another TCS constructor. Broad bridge migration is deferred: fault/cancellation Task status, ties and Fable semantics need separate tests.

## Resume Phase 2

1. Re-read brief/rules/report; confirm machine permission before any dotnet activity. Do not infer permission from this handover.
2. Tests first, commit tests before implementation; preserve all existing tests. Relevant files: WorkflowResourceTests.fs, WorkflowConcurrencyTests.fs, FiberObserverTests.fs, FiberDiagnosticsTests.fs, RuntimeSemanticsTests.fs.
3. Gate suspended cleanup, cancel caller mid-cleanup, assert completion waits for cleanup in acquireRelease, ensuring, race loser, explicit scope close; preserve defect composition. Primitive tests must prove behavior, not just fail compilation on a missing name. Existing behavior already passes many cleanup cases; capture behavioral failure using a broken primitive/caller-token propagation as required.
4. Throw OnStart/OnEnd/OnUnobservedDefect; check composition continues after first throws, sinks/registry/Elmish where feasible, and stable success/error/interrupt/defect exits.
5. Implement centralized tokens/reporting and target-specific Random.Shared. Avoid broad runtime refactoring and new per-call allocations.
6. Benchmark only FiberCostBenchmarks and ParallelCompositionBenchmarks against origin/main; record per-case B/op before/after. Measurements currently UNMEASURED, deliberately.
7. Handover, commit, reply PHASE 2 DONE, stop.

## Phase 3 remaining

Extend torture Scopes.fs/Fibers.fs for mid-cleanup cancellation and throwing observers; register any new scenario. All verification stages, fail-first artifact, clean HEAD-matching result.json, and done-check remain mandatory for implementation completion. Use detached `setsid -f axial-verify` per TOKEN-RULES; no verification was run or claimed during Phase 1.
