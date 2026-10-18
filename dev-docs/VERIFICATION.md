# Verification workflow

How a change is proven before it is committed and pushed, for people and coding agents. Each rule below exists
because skipping it cost real time on the 0.11 work.

## Report the script's result, not your reading of the output

Run `scripts/verify.sh` (all stages) or name stages: `scripts/verify.sh build test`. It runs, with fixed timeouts:

| Stage | What it runs |
|---|---|
| `build` | `dotnet build Axial.slnx -c Release` |
| `test` | `dotnet test Axial.slnx -c Release --no-build` |
| `fable` | `scripts/run-fable-tests.sh`, then the compiled suite again until it has run `FABLE_RUNS` times (default 3) |
| `aot` | `scripts/run-aot-probe.sh` |
| `docs` | `scripts/check-docs-conventions.sh` and `scripts/check-source-inventory.sh` |

It writes `artifacts/verify/result.json` (commit, dirty flag, per-stage status, seconds, and summary) plus one log
per stage. A task is verified when `"passed": true` for the stages it touches. Quote the summary lines in reports.
Do not treat "passed on rerun" as passing: a stage that needed a rerun is a flaky test to record below.

The Fable stage runs three times because a single pass hid a deterministic-under-load stall introduced by T1.
`dotnet livedocs test --warn-as-error` stays a phase or release step (see AGENTS.md), not part of `verify.sh`.

## Build with the SDK CI uses

`mise.toml` and every workflow pin the same .NET SDK (10.0.402). When CI floated on `10.0.x` it moved to a newer
F# compiler than local builds, which emitted invalid IL for two `task { }` shapes in `ProcessLive.fs` (a generic
`try ... with error -> ...; return raise error`, and local `let rec` functions inside the block). Every process test
failed on CI with `InvalidProgramException` while passing locally. Bump the pin in both places together.

## Checkpoint before every agent run

`scripts/checkpoint.sh "<label>"` commits the working tree as a local `WIP checkpoint:` commit and prints
`restore=<sha>`, so a bad run is undone with `git reset --hard <sha>` (with a clean tree it makes no commit and `<sha>`
is HEAD). Never push checkpoints: squash with `git reset --soft <last pushed commit>` and one `git commit`. Do not
reset to "the checkpoint's parent": when no checkpoint was made, that drops the previous real commit into the squash.

## Prove a new test can fail

A test that passes without the fix proves nothing. Reviews of 0.11 work found several: a defect raised after an F#
`async` cancellation that never ran, and a dispatch race test that never pumped the dispatcher. After committing or
checkpointing:

```bash
scripts/check-tests-fail-without-fix.sh <base> <test project> "<dotnet test filter>"
```

It restores `src/` from `<base>` in a temporary worktree, keeps the new tests, and passes only if they fail or do not
compile there. Use it for bug fixes and behaviour changes; for new APIs, compiling against old `src/` fails trivially,
so the review must check the assertions instead.

## Size tasks so one run can finish

Broad tasks ("make every async service member a flow") came back with no changes; the same work split into slices of
one interface or one module, each naming its files and its tests, finished. Split a task before starting when it
touches more than one package or needs more than one new test file. Give each slice: intent, files, the tests that
prove it, and the `verify.sh` stages to run.

## Keep measurements isolated

Benchmarks run alone: a test suite running in another worktree skewed `ScopeCloseBenchmarks` by more than the
regression being measured. Run `scripts/run-benchmarks.sh` (or the filtered command in `BENCHMARKS.md`) only when no
other `dotnet` or `node` job is running, and compare allocation exactly and time within noise against the recorded
baseline.

## Known flaky tests

Record a test here the first time it fails and then passes unchanged; fix or delete it rather than rerunning past it.

| Test | Seen | Symptom | Status |
|---|---|---|---|
| `WorkflowStreamTests.debounce keeps the last value of each burst` | T20 slice 2 verify run | Expected `[3; 5]`, got `[5]` under full-suite load | Fixed: burst gaps were too close to the debounce deadline under load; zero-gap bursts and a wide virtual-time boundary separate them |
| `WorkflowSchedulingTests.Flow runtime helpers cover timeout and retry` | 2026-10-18, T4 full run | 20 ms sleep raced a 1 ms timeout under load | Fixed: manual time and a 30 s sleep separate the timeout deadlines |
| `ProcessServiceTests.run reports every non-zero stage through the typed error channel` | 2026-10-18, first `verify.sh` run | Asserted `cat` exits 0, but a failing stage kills the rest of the pipeline, so it is 0 or 137 | Fixed: the test accepts either |
| `WorkspaceTests.the workspace is removed when the gate fails` | 2026-10-18, T15 slice 3 | Diffed every `/tmp` directory, so temp directories from other test assemblies counted as leaks | Fixed: workspaces use an `axial-workspace-` prefix and the test counts only those |
| `WorkflowSchedulingTests.Flow timeout helpers work as expected` | 2026-10-18, full suite | 50 ms sleep raced 1 ms timeout | Fixed: manual time and 30 s sleeps separate the deadlines |
| `WorkflowStreamTests.switchMapFlow interrupts the flow for a superseded value` | T17/T21 verify run | Failed once under full suite; needs investigation | Open |
| `ParallelSettlementTests.zipPar interrupts a suspended sibling of a synchronous failure` | 2026-10-18, T16 slice 1 | The sibling slept 20 ms, so a slow interrupt under full-suite load let it finish | Fixed: the sibling sleeps 10 s, so it finishes only if not interrupted |
| `TortureTests.A torture scenario keeps every invariant on every round(name: "schedules")`; a stream test | 2026-10-18, T17 run; schedules again in flaky sweep verify run 2 | Schedules failed in full suite (701 passed, 1 failed); stream test failed once previously | Open: investigate schedules failure and capture seed from `artifacts/verify/test.log`; identify stream test |
