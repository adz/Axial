# Guardrails fast mode: measured design

## Decision

Use a **parse-only, in-process MSBuild task for fast builds**, with content-keyed per-file results. Keep the complete typed analyzers and policy review in an explicit full command. Do not make a persistent typed host a prerequisite for ordinary builds.

The owner accepted syntax hints plus explicit Full checks. Fast is now the package/repository default; Full remains
explicit in CI, verification and release checkpoints. The experiment below is historical, not a performance claim
for the shipping visitors. Equivalent fast coverage of all typed rules, especially AXG001, remains deliberately out
of scope. See `docs/guardrails/01-guardrails.md` for the implemented coverage contract.

## Experiment scope

- Baseline: `8ba7ce28f`, .NET SDK 10.0.402, Linux, Debug. Experiments ran in a detached temporary worktree, never product code on main.
- Projects: `src/Axial/Axial.fsproj` / net8.0 (edit `Flow.fs`), and `tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj` / net10.0 (edit `WaitForFactsAnalyzerTests.fs`). Edits append a comment, then restore the original bytes. They invalidate analysis without changing semantics.
- “Cold” means a fresh analyzer/checker process with restored packages and built references, **not** a cold filesystem or clean compilation. “Edit” is a subsequent one-file edit. CLI edit runs are still fresh processes; host edit runs retain the checker.
- Every build and measurement held the shared `dotnet.lock`, with `AXIAL_DOTNET_LOCK_HELD=1`. Background machine load was not otherwise controlled. Numbers are observations, not confidence intervals.
- The analyzer-test project intentionally does not import the production Guardrails targets. Its direct analyzer runs exercise all rules against real, typechecked test files; its normal build timings are not a production Guardrails integration benchmark. Direct CLI runs exit 254 because their default configuration reports findings, not because analysis crashed. FCS probe runs had zero type errors.
- Evidence: `/home/adam/projects/scratch/axial-coord/pi/guardrails-fast-design.*` logs, JSON, and `guardrails-fast-design-prototype.tar.gz`. The archive contains the disposable runner, task, drivers, compiler-argument snapshots and index experiment. It is not shipping code.

At this baseline, the targets **already write `FscCommandLineArgs` and use `--fsc-args-file`** when CoreCompile supplies them. Option A is not a new discovery to implement again. A forced analysis after an otherwise up-to-date compile can still take the project-loading fallback. `AxialGuardrailsTimings` has no implementation at this baseline; profiling used timestamped CLI diagnostic output, explicit probe stopwatches, MSBuild performance summaries and `/usr/bin/time`.

## Where the time goes

CLI intervals, seconds. Startup includes runtime/tool initialization and analyzer loading; project load ends at “Checking project”; FCS ends at first “Checking file”. The final interval includes file/context work, parallel rules, output and teardown; it is **not pure rule CPU**.

| Project / input / state | Startup | ProjInfo/design-time load | FCS parse + typecheck | Dispatch/rules/output remainder | Wall |
|---|---:|---:|---:|---:|---:|
| Core / project / cold | .50 | .64 | 9.23 | 16.65 | 27.03 |
| Core / project / edit | .50 | .63 | 8.15 | 16.19 | 25.47 |
| Core / compiler args / cold | .47 | 0 | 7.72 | 15.03 | 23.22 |
| Core / compiler args / edit | .48 | 0 | 8.21 | 16.12 | 24.81 |
| Tests / project / cold | .51 | 1.75 | 13.32 | 6.20 | 21.78 |
| Tests / project / edit | .51 | 1.73 | 12.97 | 6.14 | 21.35 |
| Tests / compiler args / cold | .49 | 0 | 10.40 | 6.52 | 17.41 |
| Tests / compiler args / edit | .50 | 0 | 10.74 | 5.95 | 17.19 |

Compiler arguments save project evaluation, but cannot remove the second typecheck or expensive rules. These runs do not reproduce the earlier claim that most of 39.6 seconds was outside timed CPU: core args cold used 52.53 user + 1.91 system CPU seconds across parallel work. Do not extrapolate that earlier profile to this baseline.

The direct FCS runner separately timed all 13 analyzer entry points, serially, without CLI project loading:

| Phase | Core cold | Core edit, warm checker | Tests cold | Tests edit, warm checker |
|---|---:|---:|---:|---:|
| Parse + typecheck | 7.905 | 1.561 | 10.541 | 2.282 |
| File reads, background results, contexts/typed trees | .067 | .019 | .051 | .011 |
| All rules | 14.105 | 12.359 | 6.135 | 4.077 |
| Total | 22.090 | 13.939 | 16.739 | 6.370 |

Checker/options construction was ~20 ms. Context time includes materializing typed trees; subsequent traversals belong to rule time. Cold core rule costs included AXG001 1.695 s, AXG006 1.051 s, AXG008/scope 7.244 s, AXG012 2.318 s and suppression integrity 1.557 s. AXG003/004/009/013 together took .106 s, **after typechecking**; that does not make them all syntax-only.

Current integrated core build: cold analysis build 31.52 s, edit 31.37 s, no-op 1.03 s. Its edit target spent 22.823 s in analysis, .345 s fingerprinting, .009 s policy-cache validation. No-op still spent .284 s fingerprinting despite skipping the analyzer. First policy check cost .660 s; cached checks did not launch it. Thus stamps/IO are measurable independently of FCS, and should not be left unchanged in a “~0 no-op” redesign.

## Options measured

Times below are analyzer overhead/components unless explicitly labelled whole build. RSS is resident memory, not allocation; process maxima are not aggregate process-tree memory.

| Option | Cold | One-file edit | No-op | Memory observed | Conclusion |
|---|---|---|---|---|---|
| A: CLI with compiler args | Core 23.22 s; tests 17.41 s | 24.81 / 17.19 s | Skip CLI with stamp; current core fingerprint .284 s | CLI peak .69–.96 GiB across args runs | Necessary full-mode optimization, not a fast mode |
| B: retained checker + all rules, request/response host | Core 21.89 s; tests 16.67 s, plus ~.10 s host startup | 13.99 / 6.72 s | .0023 / .0021 s source-hash cache hit | Host snapshots 434–560 MiB | Warm FCS alone misses target by an order of magnitude |
| C: in-process parse + actual AXG004 raw fixture visitor | All source files .917 / .646 s task time | Fresh-build-process single-file parse/visitor ~.48–.55 s | Task skipped: whole builds .687 / 1.608 s vs baseline .690 / 1.668 s | Single-file task MSBuild process ~184–194 MiB; current core no-op baseline ~139 MiB | Only measured path plausibly within budget; coverage is deliberately narrower |
| D1: warm typed results, rules only on changed file | Still requires initial typed project + rules | Rules alone 4.477 s core / .139 s tests, **plus** recheck 1.56 / 2.28 s | Reuse cached diagnostics | Direct runner peak ~583 / 595 MiB | Misses target; changed-file-only invalidation is unsound for downstream type changes |
| D2: per-context symbol range index in AXG008 | Full runner total 17.77 / 14.26 s | Full runner total 8.52 / 5.08 s | Rerunning rules still 6.64 / 3.05 s; cache required | Runner peaks ~738 / 634 MiB | Worth a separate full-mode optimization, not enough for fast mode |

C was attached after Build as an actual MSBuild task, not measured only inside an already warmed console process. Three paired comment-edit builds:

| Project | Baseline median | With C median | Difference of medians | Paired differences |
|---|---:|---:|---:|---|
| Core | 8.132 s | 8.838 s | +.706 s | +.580, +.534, +.706 s |
| Tests | 9.087 s | 9.223 s | +.136 s | +.330, +.392, +.136 s |

An earlier unpaired core sample added 1.74 s. This is not a hard latency guarantee; require repeated end-to-end acceptance measurements after adding more visitors, hashing and diagnostics. C's all-file cold result excludes MSBuild startup and graph evaluation. In a warm console, parsing the edited file plus fixture visitor took .034 s core / .005 s tests; quoting only those numbers would conceal task/FCS loading and JIT.

B used a long-lived .NET subprocess with stdin request/response, retained checker/options, source-content hashes, and a 30-second idle exit. It is a **state/latency prototype**, not the proposed concurrent transport or a production-valid reference/config cache. C only runs the fixture visitor; additional fast visitors below remain unmeasured. D2 indexed symbol uses by source end position instead of repeatedly scanning every symbol for every AST call in AXG008: that rule fell from 7.244 to 1.039 s cold core and 3.687 to .492 s cold tests. Diagnostic equivalence and retained-memory bounds still need tests before adopting it.

### Coverage and implementation risk

| Rule | Fast proposal | Full mode / reason |
|---|---|---|
| AXG001 effects | Syntax candidates for direct catalog spellings, preserving the replacement guidance; explicitly labelled unverified hints | Existing symbol matching, aliases, overloads, scope and completed-read exceptions require typed facts |
| AXG003 raise in flow | Direct `raise` in syntactically identified `flow { }` bodies; generic guidance only | Typed error-type advice; helper/`Flow.delay` reachability remains a separate rule improvement, not solved by full mode today |
| AXG004 fixtures | Reuse the measured untyped fixture visitor | Same syntactic semantics, including attributes and nesting |
| AXG006 reflection formatting | Obvious `%A`/`%+A` syntax candidates only | Type-dependent interpolation, union/record ToString and actual reflection targets |
| AXG009 raw concurrency | Direct qualified constructor/API syntax candidates | Actual symbol identity and typed classification |
| AXG013 guessed waits | Direct qualified Task.Delay/Async.Sleep syntax candidates in configured test-role files | Existing symbol matching, cancellation waits and timeout constants |

Syntax candidates must not masquerade as exact existing diagnostics: attach a `fast/unverified` classification, do not block builds on ambiguous names, and retain the owning AXG rule's guidance. Shadowing, aliases and inactive conditional-compilation branches need explicit tests. Do not pass dummy typed contexts to existing analyzers in shipping code; extract parse-only visitors behind a separate contract. Full mode runs every existing analyzer, including suppression integrity and rules not listed here.

A supports all rules but retains startup, FCS and CLI failure modes (low incremental implementation complexity, since already present). B supports all rules but adds high-complexity process ownership, stale-state, IPC, memory and crash concerns. C has moderate implementation complexity, reduced typed coverage, and MSBuild load-context/version-conflict risk. D1 has high invalidation complexity; D2 is a localized optimization but does not prove the target. Neither permits treating an unchanged file as semantically unchanged after an earlier file or reference changes.

## Execution and failure contract

1. **Selection is explicit.** Add `AxialGuardrailsMode=Fast|Full`, default Fast after the owner's acceptance in both package targets and this repository. Fast runs parse-only advice; Full runs the exact typed checks. Resolve test roles and enabled rules from checked-in policy; never from `CI`, environment heuristics, branch names, or Release configuration. Explicit command/property overrides are user authority, not inferred policy.
2. **Full command.** Extend today's policy-only `axial check` with `axial check --full`: obtain compiler argument snapshots for each project/TFM, run every rule, then `axial policy review --check`. It must not approve policy or generate/approve baselines. Checked-in release workflow explicitly invokes this command and requires complete coverage. `dotnet build -p:AxialGuardrailsMode=Full` runs all rules and the ordinary policy drift check; `axial check --full` additionally enforces policy review. Full is not implicitly selected by `-c Release`.
3. **Fast runs synchronously.** Parse changed files after compilation with compiler defines, language version and ordered source lists; no ProjInfo and no second typecheck. Reuse immutable checker/parser setup within an MSBuild node, isolate FCS dependencies in a collectible/load-isolated task context, and bound cache memory. Parse errors or unsupported syntax produce a skipped-file warning, not a clean result. No background typed job silently changes build outcomes later.
4. **Failures are distinct from findings, and fail closed.** Catch failures per analyzer/file and continue the remaining checks. Aggregate infrastructure failures and incomplete coverage into one `AXGINF001` build error per project/TFM, identifying causes and skipped checks. Task-load, host and process failures also fail the build, including under warnings-as-errors. There is no opt-out. Finding severities remain policy-controlled; incomplete coverage is recorded explicitly and never stamped as successful. This owner decision landed in slice 1 and supersedes the original warning-only proposal.
5. **No successful stamps after skips.** Cache structured findings and explicit coverage, not merely exit code 0. Fingerprint mode, analyzer/task/FCS versions and binaries, ordered compiler args including defines, TFM, source contents/order, relevant reference contents, effective role/rules, policy/baseline/suppression inputs and schema version. Distinguish fast from full coverage. Atomically publish only complete results for that exact request; corrupted/truncated/mismatched entries are misses. Replay cached findings so a cache hit does not hide diagnostics. Never let a fast result satisfy full coverage.
6. **Cheap no-op gate.** Keep per-file content digests and immutable tool digests, avoiding today's rehash of every tool/reference on every build. File metadata may accelerate lookup only with a documented invalidation guarantee; changed metadata triggers content hashing, not a successful result. Do not trust metadata alone where preserved-timestamp edits are possible: rehash mutable source/config inputs; memoize immutable package/tool inputs by verified identity. Measure the resulting gate separately and aim for <50 ms per project. This target is not yet demonstrated by the production fingerprint implementation.
7. **Optional host later.** If a host is introduced, key it by canonical worktree root, SDK/FCS/tool identity and protocol version; separate project/TFM/config request keys. Use a user-private Unix socket/named pipe, atomic startup ownership, bounded queue, per-key serialization and request IDs. Validate source/reference/config fingerprints before and after work; discard stale responses. On stale state, timeout, incompatible protocol or crash: warn, evict and run cold with the exact captured inputs; if that fails, skip without stamping success. Idle exit releases memory; concurrent worktrees never share mutable checker/config state. Cold fallback is not allowed to silently drop rules or change finding severity. Keep this out of the initial fast path.

The cold-host fallback cannot promise <1 second. The selected default avoids this dependency instead of disguising fallback latency.

## Reviewable migration slices

1. Add phase timings and stable full-coverage reports without changing selection. Tests cover fingerprints, cache corruption, warning replay and partial failures; capture a repeatable core/test edit benchmark.
2. Introduce explicit mode plumbing and checked-in release `--full` invocation, initially retaining Full as rollout default. Test mode separation, no environment switching, role selection, all-project/TFM coverage and policy review without approval.
3. Extract AXG004's syntax visitor; ship isolated in-process task behind opt-in Fast. Test task-load/parse crashes, warnings-as-errors, changed defines, stale stamps, conditional syntax and concurrent builds. Require repeated <1 s added edits and <50 ms no-op gate, not just visitor microbenchmarks.
4. Add AXG001/003/006/009/013 syntax guidance individually, with shadowing/alias tests and explicit fast/full coverage differences. Compare advice against full diagnostics. Re-measure after each visitor; obtain owner acceptance of reduced typed coverage before switching the checked-in default to Fast.
5. Optimize full rules using shared per-file symbol/range indexes (AXG008 prototype first), with diagnostic-equivalence and memory-retention tests. Consider a warm host only if explicit full checks remain too slow; it is not needed to ship fast feedback.

The original experiment was design-only; subsequent implementation changed the Fast default, documentation and release notes without editing the policy lock or baseline. `dev-docs/**` planning notes require no `scripts/verify.sh` run; implementation slices above require the repository's fail-first and full verification workflow.
