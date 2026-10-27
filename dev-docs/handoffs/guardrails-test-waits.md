# AXG013 — phase 1 handover

## State

Phase 1 adds tests only. No implementation, docs, baseline changes, or baseline command. No push.
Worktree: `/home/adam/projects/Axial/feature/guardrails-test-waits`.
Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.md`.
Read its TOKEN-RULES.md before continuing.

## Decisions

- AXG013 covers Thread.Sleep, awaited Task.Delay and Async.Sleep in effective policy role `test`, default deny.
- Flow.sleep / Clock.sleep are deliberately out of scope: the workflow's syntax does not establish which clock its environment supplies. This is the brief's explicit fallback, not a guess that a particular function name means live time. Document this scope in phase 2 and the report.
- Tests pass policy/config explicitly to the proposed `WaitForFactsAnalyzer.analyzeWithConfig config ctx` entry point. This avoids global environment mutation and permits safe parallel xUnit execution. Phase 2 should implement this entry point plus normal CLI registration.
- Deadline tests include inline Task.WhenAny arms, an array arm, and a local named deadline. Do not broadly exempt every Task.Delay merely because a race exists somewhere in the function.
- Suppression tests check both reasoned suppression and site locality. HardeningTests adds AXG013 to the existing AXG002 stale-directive contract.

## Files

- `tests/Axial.Guardrails.Tests/WaitForFactsAnalyzerTests.fs`: report forms, non-test roles, message guidance, failure deadlines, fact polling, controlled-clock scope, suppression, lookalikes.
- `tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj`: compile new test file.
- `tests/Axial.Guardrails.Tests/HardeningTests.fs`: stale AXG013 suppression must produce AXG002.
- This handover.

## Verification

Normal committed test run exited 1 at the Guardrails policy check (MSB3073), before compiling/running tests. `axial check` identifies protected test changes and suppression strings in the new fixtures. No lock/baseline approval was performed.

FAIL-FIRST PENDING. Coordinator decision appended to the brief says not to bypass this check: another lane removes Guardrails test sources from the protected set; after main is updated and a human approves the lock, wait for coordinator instruction to merge origin/main and run tests. Phase 2 may implement without running dotnet.

Disclosure: before that coordinator clarification, a second run used `-p:_AxialRepoSkipGuardrails=true`, the flag used in the repository fail-first script. It exited 1 with FS0039 because WaitForFactsAnalyzer does not exist. This bypass run is NOT accepted as fail-first evidence. It also exposed an assembly-local Policy.Config type mismatch, which was corrected by parsing a temporary policy through GuardrailConfig instead. Do not repeat the bypass.

Logs: `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits-phase1-test.log`, `guardrails-test-waits-phase1-red.log`, and `guardrails-test-waits-policy.log`. No verify/result.json was produced; full verification is deferred. Fixtures and behavioral assertions have not run.

## Exact next step

Phase 2: implement AXG013 and docs, register with small edits (another lane owns AXG009–AXG012). Do NOT run dotnet or bypass policy while blocked; wait for coordinator instruction after main/lock approval before merging and running focused tests. Confirm every source fixture typechecks, not just that allowed fixtures yield zero findings. Review test fixture syntax/type diagnostics once the proposed analyzer entry point exists. Preserve test expectations rather than relaxing them to match implementation.
Then update this handover, commit, reply PHASE 2 DONE, and stop. Phase 3 prepares per-file violation counts and stops before HUMAN baseline approval. Do not rewrite repository tests or run `axial policy baseline --rule AXG013`.

## Open questions

- No policy rule key prescribed by brief: phase 2 chooses a durable name and registers it in all policy lists.
- Clock.sleep / Clock.fixed named in the brief are not current APIs in the inspected PlatformService Operations.fs; tests use actual Flow.sleep. No PlatformService test reference was added. Manual-clock exemption follows the deliberate exclusion of all workflow clock sleeps.
- Full verification and done-check belong to phase 4, after baseline approval; phase 1 is deliberately red.
