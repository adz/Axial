# Fast edit → build → test feedback

**Status:** measured design, 2026-10-25. Fast local feedback is a product constraint for the development system, especially for agents that build after small changes.

## Baseline

Measurements used SDK 10.0.402, Debug configuration, warm NuGet caches, binary logs, `-clp:PerformanceSummary`, and ISO timestamps. Each scenario ran three times; the table reports the median of MSBuild's `Time Elapsed` (excluding time in this workspace's shared `dotnet` lock). A source scenario made a semantically neutral one-line change to `Flow.succeed`; the API scenario added one public value; the test scenario added one comment to `PolicyTests.fs`. Files were restored after each experiment. The first no-op was a 48.48 s cold-ish outlier; the other runs were 13.51 and 11.99 s.

| Command after edit | Runs (s) | Median | What rebuilt |
|---|---:|---:|---|
| `dotnet build Axial.slnx` (no edit) | 48.48, 13.51, 11.99 | **13.51 s** | Normally no F# compilation or analyzer execution; solution evaluation still dominates |
| same, body-only `Flow.fs` edit | 378.27, 462.18, 361.70 | **378.27 s** | Axial plus 17–56 Fsc invocations across downstream TFMs/projects; all 54 analyzer targets considered |
| same, public API edit | 398.01, 411.16, 437.94 | **411.16 s** | 55–57 Fsc invocations: effectively the full downstream graph |
| `dotnet test tests/Axial.Tests` after body edit | 177.54, 156.20, 164.96 | **164.96 s** | Axial and the 7-project test closure, then 478 tests |
| same after test-only edit | 92.13, 78.86, 112.93 | **92.13 s** | Primarily Axial.Tests, then 478 tests |

The large run-to-run spread is real machine contention. Medians are adequate for ranking changes, not for claiming single-digit percentage improvements. Repeat acceptance measurements on an otherwise idle machine.

### Where the time goes

Performance-summary task times are aggregate across parallel projects and therefore can exceed wall time. They show attribution, not additive elapsed time.

| Scenario | Restore | Fsc aggregate | Guardrails aggregate | Test host/execution |
|---|---:|---:|---:|---:|
| no-op solution | restore target ~3.9 s; task 0.47 s | normally 0 | ~0 (up-to-date checks only) | — |
| body build | task 0.49 s | **458 s** median | **631 s** median | — |
| API build | task 0.48 s | **637 s** median | **697 s** median | — |
| body + Axial.Tests | task 0.21 s | **33 s** median | **112 s** median | VSTest task **48 s**; reported test duration 45–49 s |
| test edit + Axial.Tests | task 0.21 s | **18 s** median | **23 s** median | VSTest task **43 s**; reported test duration 41–48 s |

Project evaluation/no-op traversal is the ~12–14 s floor for 57 projects. After a core edit the slowest downstream paths are `Axial.Process`, `Axial.HttpClient`, `Axial.Hosting`, `Axial.Files`, `Axial.FileSystem`, `Axial.Telemetry`, `Axial.Console`, and `Axial.PlatformService`; tests/examples then fan out behind them. Guardrails is currently an external CLI per affected project. Each invocation reloads and type-checks that project independently, duplicating compiler work. This is the largest controllable cost. Test execution itself, not discovery output, accounts for about 43–50 s; process startup/discovery is only the small remainder.

## Option assessment

| Option | Evidence / estimate | Result identity | Decision |
|---|---|---|---|
| `ParallelCompilation` / `--parallelcompilation` | Setting the MSBuild property caused FS0075: “option is for test purposes only.” MSBuild already builds independent projects concurrently. | Unsupported | **Reject.** Do not ship a compiler test switch. |
| Graph-based type checking | FCS/editor feature; compiler exposure is experimental (`--test`), not a supported build contract. Best case helps large single-project checking, while most measured cost is repeated projects/analyzer hosts. | Not supportable yet | Track upstream; do not gate normal builds on it. |
| `ProduceReferenceAssembly` | Axial already emits `artifacts/obj/Axial/*/ref/Axial.dll`. The body/API gap is only 33 s and body edits still fan out, so stable reference output is not currently preventing downstream work. F# reference assemblies are intended to omit bodies, but timestamp/up-to-date and Guardrails fingerprints must be proven together. | Should be identical; needs hash/rebuild test | **High-priority experiment:** hash ref DLL after body edit and inspect downstream Fsc calls. Fix copy/up-to-date inputs rather than merely setting an already-effective property. Estimated 150–250 s saved on body builds if fan-out stops. |
| Deterministic builds | SDK default is deterministic. It is required for reproducibility but does not make an up-to-date check semantic; no expected latency win. | Identical | Keep enabled; no feedback project. |
| MSBuild server / node reuse | Can save process/JIT/evaluation seconds. Current agent lanes deliberately disable reuse to cap memory, and the 12–14 s floor is small beside type-checking. | Identical | Optional human-local lane only; estimate 2–8 s. Never silently enable in constrained agent lanes. |
| `dotnet build -graph` | Solution traversal already schedules project references in parallel; graph mode may reduce orchestration overhead but cannot remove required compiles or CLI analyzers. | Identical | Benchmark after larger wins; estimate 0–10 s. |
| In-compiler analyzers | FSharp.Analyzers.SDK is designed for FSAC/editor and its CLI host. It does not provide a Roslyn-style supported hook inside `fsc`; Axial's current build deliberately invokes `fsharp-analyzers` after build. | No supported path | Reject pending upstream compiler integration. |
| Persistent FCS Guardrails host | One daemon can cache project options, references and checked projects, then run the same SDK analyzers. Against 631–697 s aggregate analyzer work, even 70% reuse could remove **2–4 wall minutes** on a core edit. | Must be byte-for-byte diagnostic-equivalent to CLI | **Project.** Protocol must key cache by all current fingerprint inputs, cancel stale requests, isolate analyzer versions, recover after crashes, and retain a one-shot CI mode. Medium/high cost and correctness risk. Editor sharing is a later optimization because FSAC version/lifetime coupling raises risk. |
| Test impact analysis (TIA) | Axial.Tests always executes 478 tests for a comment. A file→test map could reduce common loops from 43–50 s execution to seconds. Project-level selection is safe but coarse; file/test selection is heuristic. | Not identical coverage locally | **Project.** Local only; always full CI/verify. Start with explicit project graph, then recorded test dependencies plus conservative fallback. |
| Persistent/in-process xUnit v3 host | Startup/discovery is small compared with the 41–49 s test body. Reusing process state also risks leaked globals and order dependence. | Potentially different isolation | Defer; likely <5 s saved. Use only with periodic clean-host comparison. |
| Parallel test projects | Helps full solution tests, not the single Axial.Tests edit loop. Memory contention can erase gains. | Identical if isolated | Add a bounded local lane after measuring; CI can retain current authoritative layout. |
| Split slow suites | Process/integration and Guardrails MSBuild fixture tests involve real processes/builds and belong outside a core unit loop. Axial.Tests itself still takes ~45 s, so tag/split genuinely slow tests based on per-test data, not names. | Identical across complete lanes | Quick organizational win: fast lane on edit, complete lane before checkpoint/CI. |

## Recommended order

| # | Work | Saving | Cost / risk | Identical? |
|---:|---|---:|---|---|
| 1 | Add supported scripts for `build-core`, `test-core-fast`, and `test-affected`; print when the command is non-authoritative. Avoid `Axial.slnx` in a core edit loop. | Immediate: **~213 s** versus solution body build; test filtering can save part of 43–50 s | Low; risk is developer misunderstanding | No for fast tests; full lane remains identical |
| 2 | Diagnose reference-assembly fan-out with DLL hashes and binlog assertions; make body-only Axial edits leave downstream compile inputs unchanged. | Estimated **150–250 s** per solution body build | 1–3 days; medium F#/MSBuild up-to-date risk | Yes |
| 3 | Prototype one persistent Guardrails checker for a sequential project closure; compare sorted diagnostics and exit classification against one-shot CLI on every fixture. | Estimated **120–240 s** wall on core edit | 1–2 weeks; high invalidation/lifetime risk | Required: yes |
| 4 | Separate fast tests from process/build suites using measured per-test timings; run projects concurrently with a memory cap. | Estimated **10–30 s** for common core loop; larger on solution tests | 2–4 days; low/medium flake risk | Yes when all lanes run |
| 5 | Add conservative TIA: changed project closure first, opt-in file→test mapping second, with unknown changes forcing the full suite. | Common test edit: up to **40 s** | 1–2 weeks; stale-map risk | No locally; yes in CI |
| 6 | Benchmark MSBuild server/node reuse and `-graph` on an idle machine. | Estimated **2–10 s** | Low; memory risk | Yes |
| 7 | Reconsider persistent xUnit host and graph checking only after attribution shows startup/compiler checking remains material. | <5 s / unknown | Medium/high | Uncertain |

### Incremental verification contract

`scripts/verify.sh` should gain an explicitly non-authoritative incremental mode that reads the merge-base diff. Documentation-only changes run conventions/inventory; core source changes build Axial plus its changed project closure and run the mapped fast tests; operational-package changes build/test that package closure; Guardrails changes always run analyzer unit and MSBuild fixture tests. Changes to shared build files, package versions, solution files, analyzer configuration, generated-input rules, or an unknown path force full verification.

CI and release checkpoints must always run the existing clean Release build, complete tests, Fable, AOT, and docs stages with Guardrails in one-shot mode. Incremental success is latency feedback, never release evidence. Store the selected paths, dependency rule version, and fallback reason in `artifacts/verify/result.json` so an agent cannot mistake partial coverage for full verification.
