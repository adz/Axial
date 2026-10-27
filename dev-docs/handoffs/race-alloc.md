# race-alloc — phase 3 verification handover

## Phase 3 scope and final verification procedure

Checkpoint: `restore=0ea0b3d40`; clean `perf/race-alloc`. No tests, examples, policy, lock or verification
scripts changed in phase 3. Chose the brief's permitted torture waiver, recorded in ignored
`artifacts/verify/torture-waiver.txt`: `parallel` stresses repeated race scheduling and both exit handlers, but its
post-race finish gates do not pin return ordering. The unchanged committed ownership/settlement/observer tests
supply that boundary coverage, including the phase-2 deliberate-mutation failure. No new tests means the done-check
correctly skips baseline fail-first; do not manufacture a failing behavior test for unchanged semantics.

First full verification at `8cb609ac8`: build passed (0 errors), test passed (941 passed, 0 failed,
14 projects), AOT/docs/livedocs passed; Fable failed compilation. Fable's parser rejected the extra indentation
left by removing the .NET-only `if/else` in race's two synchronous fast paths. Corrected `src/Axial/Flow.fs`
with explicit `#else` fold expressions at the enclosing indentation. Neither target's execution path changes;
.NET allocation machinery remains identical. This is a verification repair, not a new optimization or behavior
change. The failed result and logs are preserved externally as `race-alloc-verify-first-*`.

This handover is committed before final verification so all stages can certify the final clean HEAD. Run detached
`setsid -f axial-verify` with the persistent log `race-alloc-verify.log`, then the coordinator's done-check.
Final results, exact verified commit and any failures/reruns are recorded in the external persistent report
`/home/adam/projects/scratch/axial-coord/pi/race-alloc.report.md` and `artifacts/verify/result.json`, not backfilled
here after verification (which would invalidate the certified HEAD). If verification fails, phase 3 remains
incomplete; repair/report without weakening checks. After all stages and done-check pass, phase 3 ends: no push
and no further phase. Allocation results and the RunOnThreadPool fractional variance below remain unchanged.

## Inherited state: phase 2 complete

Checkpoint before phase 2: `restore=2aa0b40f0`. Work remains on `perf/race-alloc`; commit locally, **no push**.
Only `src/Axial/Flow.fs` and this handover changed. No public API or observable behavior change is intended;
no release-note change. Policy, lock, analyzer configuration, and verification scripts are untouched.

### Implementation and decisions

- Explicitly constrain race's local settlement functions to the enclosing value/error types. Release inspection
  no longer finds the generalized `settle` type-function factory or its `Specialize` chain.
- Add private `closeRaceScope`: .NET returns the original exit directly when `Scope.Close` completes successfully;
  otherwise await the close and combine cleanup exceptions using the existing `combineCleanup` rules. Catch
  synchronous close exceptions too. This avoids the singleton list, result ref, list enumerator and completed task.
  Fable delegates to the existing `closeScopes`, preserving its cleanup implementation.
- Factor `finishLoser` and use guarded synchronous continuations instead of allocating both outer/nested fold
  continuations on the inline path. Suspended paths still use `Execution.fold`. Both existing stack-guard boundaries
  remain. `Platform.raceExecution`, loser interruption/awaiting, runtime installation, scopes and ownership are unchanged.
- Always close the loser with `CancellationToken.None`. Keep a successful winner's scope; close a failing winner's.
  Preserve typed loser failure/interruption filtering and loser/cleanup defect combination.
- Leave RuntimeContext/RootScope layout and unrelated combinators alone. Combined measured saving: **648 B/race**;
  do not treat this as an independently measured per-edit attribution.

### Focused correctness evidence

No duplicate test was added: the brief explicitly permits citing existing committed invariant-1 tests.
`ScopedResourceOwnershipTests.race closes a right loser's scope before returning` inspects release **inside** the
caller scope immediately after race. Before implementing the fix, deliberately replaced the loser's `closeScopes`
call with `Platform.ofExit loserOutcome`. The unchanged test **failed (1 failed, 0 passed, exit 1)**. Restored the
required closure as part of the implementation; the deliberate mutation is not retained.

```bash
dotnet test tests/Axial.Tests -c Release --nologo -v q --filter 'FullyQualifiedName~ScopedResourceOwnershipTests&DisplayName~race closes a right'
dotnet test tests/Axial.Tests -c Release --nologo -v q --filter 'FullyQualifiedName~ScopedResourceOwnershipTests|FullyQualifiedName~ParallelSettlementTests|FullyQualifiedName~FiberObserverTests|FullyQualifiedName~WorkflowParallelTests|FullyQualifiedName~WorkflowConcurrencyTests'
dotnet test tests/Axial.Tests -c Release --no-build --nologo -v q --filter 'FullyQualifiedName~StackSafetyTests|FullyQualifiedName~ScopeConcurrencyTests|FullyQualifiedName~WorkflowResourceTests'
```

Final focused runs: **89 passed**, then **57 passed**; existing tests unchanged. The first set includes both winner
orientations, winner resource ownership, cancellation, exclusive socket release, forked loser fibers, suspended
selection, startup defects and loser-defect observation. The second checks scope/resource handling and stack safety.
Normal Release builds ran guardrails; no check was bypassed.

Additional persistent (uncommitted, outside-repo) probe `race-alloc-phase2-cleanup.fsx` forces loser scope closing
behind a TaskCompletionSource gate: **8 combinations passed**, covering either losing side, successful/failing
winner, and successful/defective asynchronous cleanup. It checks release completion, winner failure retention and
cleanup exception identity. This supplements, not replaces, the committed ordering tests.

### Phase-2 MemoryDiagnoser results

Same SDK/runtime/environment and commands as phase 1, with artifact suffixes `phase2-core`, `phase2-parallel`,
`phase2-comparative`. Only the filtered benchmark classes ran. Times remain indicative only on the shared machine.

| FiberCost case | Phase 1 B/op | Phase 2 B/op |
|---|---:|---:|
| ForkJoin | 2810.843 raw | 2806.252 raw |
| ForkResource | 4592 display | 4590.796 raw |
| ZipPar2 | 1280 | 1280 |
| SequencePar8 | 3704 | 3704 |
| SequencePar64 | 22296 | 22296 |
| **Race** | **2432** | **1784 exact** |
| TraversePar64 | 39360 | 39360 |
| RunOnThreadPool | 1181.344 raw | 1181.999 raw |

Race meets **<= 1800 B/op**, recovering 648 B (26.6%). All synchronous non-race allocation counts are unchanged.
Asynchronous scheduler/GC bookkeeping varies fractionally: RunOnThreadPool displays 1182 versus 1181, a raw
**+0.655 B/op**. A focused repeat measured 1181.759 B/op (+0.415); no deterministic new object or changed code path
in that case was identified. Report this variance rather than claiming exact equality for asynchronous cases.
ForkJoin/ForkResource moved downward. If the coordinator needs a strict numerical comparison for RunOnThreadPool,
repeat matched baseline/fix runs in isolation; do not alter its implementation to chase sub-byte scheduler noise.

| ParallelComposition case | Phase 1 raw B/op | Phase 2 raw B/op |
|---|---:|---:|
| sequencePar 10 flows x100 | 563610.109375 | 563610.109375 |
| zipPar x1000 | 1121212.21875 | 1121212.21875 |
| race x1000 | 2369216.4375 | 1721212.21875 |
| traversePar 10k, parallelism 8 | 2887536.046875 | 2887536 |

Comparative ZipRace exact B/op: **3353024 -> 2705024** (1000 rounds),
**33521024 -> 27041024** (10000 rounds). Saves exactly 648 B per round; neither case regresses.

Persistent logs in `/home/adam/projects/scratch/axial-coord/pi/`: `race-alloc-phase2-{mutant,focused,resource-stack,core,parallel,comparative,threadpool-repeat,inspect,cleanup}.log`.
Probe script: `race-alloc-phase2-cleanup.fsx`. Generated reports:
`artifacts/benchmarks/race-alloc/phase2-{core,parallel,comparative,threadpool-repeat}/results/`.

**Full verification, torture decision, final fail-first artifact and done-check are intentionally deferred to phase 3.**
No `scripts/verify.sh` run or phase-2 `artifacts/verify/result.json` is claimed. This phase completion is not a claim
that the full implementation slice meets the final Definition of done.

## Historical phase 1 state and boundary

Phase 1 (measurement and diagnosis) completed without source or test changes.
Worktree `/home/adam/projects/Axial/perf/race-alloc`, branch `perf/race-alloc`.
Measured HEAD and `origin/main` were both `22717db2be72e209f8c7457a4c1480520a9d6e6d`.
Checkpoint: `scripts/checkpoint.sh 'race-alloc phase 1 measurement'` returned `restore=22717db2b`.
Commit this handover only; do not push. Implementation and full verification belong to phases 2 and 3.

Brief: `/home/adam/projects/scratch/axial-coord/pi/race-alloc.md`.
Persistent report and logs: `/home/adam/projects/scratch/axial-coord/pi/race-alloc.report.md`
and `race-alloc-{core,parallel,comparative,inspect,analyzer-build}.log` in that directory.
The inspection probe is `race-alloc-inspect.fsx` there (outside the repository).

## Reproduction and baseline

Release, BenchmarkDotNet MemoryDiagnoser, .NET SDK 10.0.402 / runtime 10.0.13, x64.
The shared `dotnet` wrapper holds the coordinator's lock for each command, including its benchmark children.
Treat times as indicative only; do not make a timing claim from this shared machine.

Commands (run serially; all ultimately succeeded):

```bash
dotnet build src/Axial.Guardrails -c Release -v q -nologo
dotnet run -c Release --project benchmarks/Axial.Benchmarks -- --filter '*FiberCostBenchmarks*' --artifacts artifacts/benchmarks/race-alloc/core
dotnet run -c Release --no-build --no-restore --project benchmarks/Axial.Benchmarks -- --filter '*ParallelCompositionBenchmarks*' --artifacts artifacts/benchmarks/race-alloc/parallel
FIO_BENCH_RUNTIMES=Axial FIO_BENCH_ITERATIONS=8 dotnet run -c Release --project benchmarks/Axial.Comparative -- --filter '*ZipRaceBenchmark*' --artifacts artifacts/benchmarks/race-alloc/comparative
dotnet fsi /home/adam/projects/scratch/axial-coord/pi/race-alloc-inspect.fsx
```

Initial core/comparative builds failed with AXG001/AXG002/AXG005 because the pre-existing generated analyzer DLL
was stale: it did not understand the checked-in v2 policy's boundary declarations. Explicitly rebuilding
`src/Axial.Guardrails` fixed this. **No policy, lock, target, suppression, or source was changed; checks stayed enabled.**
The analyzer rebuild passed with zero warnings/errors; subsequent benchmark builds passed the normal checks.

### FiberCostBenchmarks — all 16 Axial/control cases measured

| Case | Axial B/op (BDN display) | .NET control B/op |
|---|---:|---:|
| ForkJoin | 2,811 | 136 |
| ForkResource | 4,592 | 136 |
| ZipPar2 | 1,280 | 584 |
| SequencePar8 | 3,704 | 1,944 |
| SequencePar64 | 22,296 | 14,336 |
| **Race** | **2,432** | 373 |
| TraversePar64 | 39,360 | 14,336 |
| RunOnThreadPool | 1,181 | 136 |

Race is exactly 2,432 B/op in the raw GC statistics, reproducing the brief's 704 B increase over 1,728.
Its displayed mean was 988.1 ns, not a timing acceptance measurement.
ForkJoin's raw allocation is 2,810.843 B/op (versus the coordinator's prior 2,809 display); asynchronous
fiber/GC bookkeeping is not perfectly integral across runs. The synchronous cases above are integral and match
that baseline exactly. No implementation changed between these runs and the coordinator's baseline.

### ParallelCompositionBenchmarks — four related cases, no whole-suite run

| Case | Raw GC bytes / measured operations (B/op) | BDN display |
|---|---:|---:|
| sequencePar 10 flows x100 | 563,610.109375 | 550.4 KB |
| zipPar x1000 | 1,121,212.21875 | 1094.93 KB |
| race x1000 | 2,369,216.4375 | 2313.69 KB |
| traversePar 10k items, parallelism 8 | 2,887,536.046875 | 2819.86 KB |

### Comparative ZipRace — Axial only, both default round counts

| RoundCount | Exact B/op from raw GC statistics | BDN display |
|---|---:|---:|
| 1,000 | **3,353,024** | 3.2 MB |
| 10,000 | **33,521,024** | 31.97 MB |

Raw GC lines are `214593536 / 64` and `100563072 / 3`. These are the phase-2 comparison ceilings.
The job requested concurrent/server GC, but BDN reported Concurrent Workstation; preserve the actual environment
and command when comparing. The coordinator's rounded displays are the same.

Generated CSV/Markdown/HTML reports are under `artifacts/benchmarks/race-alloc/{core,parallel,comparative}/results`.
They are untracked. The external logs persist across fresh sessions.

## Diagnosis: allocation machinery, not additional ownership

Relevant history:

- `61b6a479d` — **Make Flow.race await loser cleanup** (S1): changes `Flow.fs` and `Platform.fs`.
  It adds `loserExit`, `finishWinner`, `settle`, and an awaited `closeScopes` on the loser, replacing the old
  fire-and-forget `startDeed` close. It also awaits the losing execution in Platform's asynchronous paths.
- `9d04e501f` — adds `RuntimeContext.RootScope` for disconnected work. No race redesign here.
- `0cb6985bf` — releases settled disconnected fibers; not the trivial race's path.

`Flow.race` is at `src/Axial/Flow.fs:2100–2146`; `closeScopes` at 176–193; `branchIn` at 450;
`Platform.raceExecution` at `src/Axial/Platform.fs:1383`, with `decideRace` at 1332.

For this benchmark both `Flow.ok` branches settle inline. It takes `decideRace`'s
`loser.IsCompletedSuccessfully` arm. **No `awaitRaceLoser` task, Task.WhenAny, Deferred, new finalizer registration,
or new branch scope introduced by S1 is involved in that arm.** Both branch scopes already existed before S1;
they stay empty and attach lazily, so they do not register with the parent. The cancellation source is also
pre-existing race machinery, not a new S1 cost.

The Release DLL's IL exposes an unexpectedly expensive inference detail:

1. Local `settle` is generalized over otherwise unconstrained outcome types. F# emits `FSharpTypeFunc`
   specialization objects `settle@2120-4` (56 B), `settle@2120-4T` (64 B), and
   `settle@2120-4TT` (72 B). `race@2104.Invoke` creates the factory once and calls the two-stage
   `Specialize` chain **twice**, once for each outer fold continuation: **56 + 2 × (64 + 72) = 328 B/race**.
   These are actual `newobj`/`Specialize` sequences in Release IL, not hypothetical source closures.
2. The outer fold allocates `race@2146-1` and `race@2146-2`, **56 B each**. The nested settlement fold
   allocates `settle@2128-2` and `settle@2129-3`, **56 B each**, even when both branches and the close finish inline.
   The old race also had outer fold continuations, so do not count all 224 B as a net increase.
3. The new loser close uses generic list-based `closeScopes`. A delegate to the **actual private compiled method**,
   invoked 100,000 times with a reused singleton list, typed Exit, and empty/already-closed Scope,
   allocates **136 B/call**: a 24 B result ref, 40 B list enumerator, and 72 B completed `Task<Exit<...>>`.
   An empty list still costs 136 B. Independently, `Scope.Close` on the empty/closed scope allocates **0 B**.
   The race also constructs the **32 B singleton scope list** at the call site.
4. S1 adds the **24 B loserExit ref** and its **24 B callback**, and additional success/failure outcome wrapping
   and guard continuations. Existing `leftWon`, loser `Some exit`, branch contexts/scopes, and cancellation setup
   are not newly introduced objects. The removed `closeLoser`/`startDeed` closures offset part of the new cost.

The inspection probe records constructor call sites, fields, and measured uninitialized closure sizes. It is
measurement-only and does not rewrite library source. Its Exit is explicitly annotated `Exit<int,string>`:
an unannotated generic Exit in a probe itself gets rematerialized and incorrectly adds 24 B to each measurement.

These findings locate the growth in S1's generic specialization and settlement scaffolding. **Do not sum the
above gross object sizes and call the result the exact historical +704:** some replace old allocations, and
`RootScope` is a separate change. We did not rebuild historical commits for a complete net object census.
`RootScope` adds one reference per RuntimeContext (8 B on this host); the root plus two race branch contexts
would explain 24 B of race's total delta, while root plus one shared zip/sequence branch explains the common
+16 B. Leave that independent context-layout cost alone. The measured 704 B total regression is established;
the individual historical net contributions beyond the identified objects are not separately measured.

## Proposed phase-2 fix, in order

Budget: remove at least **632 B** from 2,432 to reach the 1,800 B ceiling without changing behavior.

1. Pin `finishWinner` and `settle` to `Exit<'value,'error> -> Execution<'value,'error>` (and pin the loser
   outcome where needed), or lift them to explicitly typed private helpers. Eliminate the 328 B type-function
   factory/specialization graph rather than assuming source-level lambdas accurately describe emitted objects.
   Measure the generated IL again. Annotations alone are unlikely to meet the full budget.
2. Use a typed, single-scope settlement helper rather than list-based `closeScopes` for race. Preserve its
   try/catch and cleanup-cause composition, but return a synchronous Execution when `Scope.Close` is complete;
   allocate an awaiting state machine only when cleanup actually suspends. This targets another 168 B
   (136 helper + 32 singleton list) on the empty-scope path without skipping closure of the loser.
   Prefer a race-local/isolated helper first so unrelated combinators cannot regress.
3. Factor the remaining nested fold continuations/captured state, or split synchronous and suspended settlement
   paths as existing parallel code does. Another ~136 B beyond steps 1–2 is needed. Keep stack guarding,
   runtime restoration, exception capture, Fable behavior, and asynchronous cleanup fully intact.

Do **not** restore background loser cleanup, drop loser defects, change typed loser-failure treatment, replace
caller interruption, or omit close just because the benchmark loser happens to have no resources. A successful
winner keeps its scoped resources; a failing winner closes them. Loser defects (including cleanup defects)
fail a successful winner or combine with a failing winner; typed loser failure and requested interruption alone
do not replace the winner. Close the loser with `CancellationToken.None` as today.

## Existing behavioral coverage to cite or strengthen before editing source

`tests/Axial.Tests/ScopedResourceOwnershipTests.fs`:

- 103: `race keeps the winner's scoped resources and releases the loser's` — inspects loser release immediately
  after the race inside the caller scope, and winner release only after caller scope close.
- 133: `race closes a right loser's scope before returning` — gates acquisition with Deferred and inspects
  release immediately after race.
- 156: `race releases an exclusive socket before returning` — rebinds the same exclusive endpoint after race.
- 185: `an interrupted race closes both branch scopes before returning` — gates both starts before interruption.
- 417: `A fiber forked in a race loser is interrupted with the loser`.

Also `ParallelSettlementTests.fs` (inline/suspended selection and throwing startup),
`FiberObserverTests.fs:212` (`Race loser defect is returned and not observer-reported`), and
`WorkflowParallelTests.fs` (race cancellation). Run existing tests unchanged.

The ordering tests exist; the brief permits citing them rather than inventing duplicates. In phase 2 prove the
relevant test fails on a deliberately broken variant if adding/tightening one, and commit tests before the fix.
Consider strengthening delayed/asynchronous finalizer settlement coverage if existing tests do not pin that path.

Torture: `examples/Axial.TortureTest/Parallel.fs:91–130` runs races and checks both exit handlers, but awaits
both finish signals after racing; that alone can hide early return. Prefer tightening it to assert loser cleanup
has happened at the race-return boundary (and include scoped/asynchronous cleanup), rather than waiving it.

## Files touched and validation scope

Tracked change in phase 1: **this handover only**. Source, tests, policy files, lock, and `scripts/verify.sh` untouched.
Generated build/benchmark files stay ignored. External measurement script/logs/report are outside the worktree.
No full `scripts/verify.sh`, fail-first proof, torture edit, or implementation done-check in this measurement-only
phase; the brief explicitly assigns final verification and done-check to phase 3. Maintainer planning notes alone
are exempt from full verification. Phase 1 is not a claim that the implementation slice is done.

## Exact next step (fresh session, phase 3 only)

1. Read the brief, TOKEN-RULES and this handover; confirm clean `perf/race-alloc`, checkpoint.
2. Make the required torture decision **before final verification**: tighten `examples/Axial.TortureTest/Parallel.fs`
   to observe loser cleanup immediately at race return (including asynchronous scoped cleanup), or write
   `artifacts/verify/torture-waiver.txt` naming the existing `parallel` scenario and explaining why its stress plus
   committed ordering tests suffice for this allocation-only change. The current scenario awaits finish gates after
   racing, so do not falsely claim it independently pins the race-return boundary. The brief explicitly permits a waiver.
3. Existing invariant tests were cited, not changed: the normal fail-without-fix script against origin/main would
   pass because this optimization intentionally preserves behavior. Preserve the mutation-test log as ordering proof.
   The done-check skips fail-first when tests/examples are unchanged. If phase 3 changes tests/torture, record honest
   fail-first evidence and coordinate the behavior-preserving optimization exception rather than fabricating a red run.
4. Commit any tracked changes/handover update before verification. Run **all stages** via
   `setsid -f axial-verify > /home/adam/projects/scratch/axial-coord/pi/race-alloc-verify.log 2>&1`, then poll
   `artifacts/verify/result.json` (sleep 300). Never use nohup. Ensure HEAD and dirty=false match; rerun after later commits.
   Phase 3 must check Fable and AOT as well as solution build/tests/docs/livedocs. On timeout (124) or a timing-sensitive
   test, rerun axial-verify once per the brief before debugging. Do not modify policy, lock or scripts/verify.sh.
5. Run `/home/adam/projects/scratch/axial-coord/done-check.sh "$PWD"`; report its outcome and result.json. Note the
   RunOnThreadPool sub-byte variance explicitly if not remeasured. Update the persistent external report with final
   commit, evidence and results. Update/commit this handover as required, reverify that final HEAD if it changed.
6. Commit locally, no push; reply `PHASE 3 DONE` only when that phase's required checks pass, and stop.

**Phase 2 stops here. Do not start phase 3 in the phase-2 session.**
