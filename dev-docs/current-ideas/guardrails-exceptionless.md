# Guardrails: rules without exceptions (draft, 2026-10-27)

Status: proposal awaiting the owner's decision. Supersedes the "per-site suppression + lock approval" direction in
[guardrails-protected-files.md](guardrails-protected-files.md) for new rules. The batch prepared on
`chore/guardrails-batch` (84 new suppressions, 88 baseline candidates) is on hold and is not approved.

## Problem

The goal of Guardrails is better, safer, correct software with a minimal state space, not blocking agents.
An error-level rule that sometimes needs an `axial-allow` comment is not a rule. It is a negotiation, and every
exception needs a reason, a lock, an approval ceremony and review. The AXG009–AXG013 inventory showed:

| Why a site needed an exception | Examples | Count (approx.) |
|---|---|---|
| The rule ignores where the code lives | Console/clock in hosts; raw TPL in the runtime; OS bridges in adapters | 48 host AXG001, ~29 AXG009, most `src` AXG001 |
| A primitive is missing | 25 × AXG011 "cleanup must finish after caller cancellation"; AXG010 "observer/defect sink must not replace the outcome" | 25 + ~20 |
| The rule guesses from syntax | AXG010 swallowed exceptions, AXG012 discarded work, AXG013 timed waits (repeated narrowing) | all of them |

## Principles

1. **An error holds without exception.** If a legitimate case exists, the rule's scope or the code's structure
   is wrong. Fix one of those; never annotate the site.
2. **Scope by architecture.** Rules apply per role (domain, services, capabilities, runtime-core, host, test).
   Raw concurrency, ambient effects and `CancellationToken.None` belong only to runtime-core and capability
   modules. They are errors everywhere else, with no exceptions.
3. **Move the code, not the rule.** A legitimate OS bridge inside an adapter moves into that adapter's small bridge
   module, which has the capabilities role.
4. **Name repeated reasons as primitives.** When many exceptions share one reason, add a runtime function for it.
   Examples: an uncancellable cleanup runner, and a "notify without letting the observer's failure escape" helper.
   Call sites use the primitive; the raw API stays in runtime-core.
5. **Heuristic rules are warnings.** AXG010/AXG012/AXG013 cannot be exact, so they report and do not block.
   Warnings are always fixed by changing the code; a warning that cannot be fixed means the rule is wrong, and the
   rule is changed.
6. **Baselines are a to-do list.** They only shrink, are fixed in code, and are never a permanent exemption.
7. **Less machinery.** With no suppressions for error rules, the lock only protects the TOML (roles and levels).
   The suppression scan, text-keyed identities and the per-suppression approval go away.

## Slices (proposed order)

1. **Primitives:** uncancellable cleanup; safe observer/defect notification. Replace the AXG011/AXG010 sites in
   `src/Axial`. Include torture coverage.
2. **Bridge modules:** in each adapter (Process, WebSocket, Files, Hosting, HttpClient, PlatformService), move the
   raw TPL/OS calls into one bridge file with the capabilities role, so the rest of the adapter is Flow only.
3. **Rules as role scopes:** AXG009 and AXG011 as errors outside runtime-core and capabilities. AXG010, AXG012 and
   AXG013 as warnings. Existing warnings are fixed or listed in a shrink-only to-do.
4. **Remove per-site suppressions** for error rules, then drop the suppression part of the lock.

## Kept from the halted batch

- `host` role allows ambient effects (principle 2 already).
- The AXG009–AXG013 analyzers and their tests (demoted per principle 5).
- The readable `axial check` summary.

## Review of the halted batch's exceptions (2026-10-27, owner-directed)

Owner decisions: build it. Rules guide rather than block: during development they are warnings with advice
("consider X"). An inline `// axial-note AXGnnn: <reason>` acknowledges a finding but keeps it in the review list.
At checkpoints (release build, pre-release verify), a report lists every finding and acknowledgement, and the
release fails only on unreviewed findings. There is no lock or approval ceremony for findings.

Why each group of exceptions existed, and how it goes away:

| Rule | Sites | Reason given | Resolution |
|---|---|---|---|
| AXG009 | ~20 | lock/Interlocked around counters and callback snapshots | Drop lock/Interlocked/Monitor from AXG009; our bugs were TCS/Task.Run/ContinueWith/CTS lifetimes |
| AXG009 | 4 | lock around a shared Random | Use `Random.Shared`; delete the locks |
| AXG009 | ~5 | Task↔Deferred bridges, Hosting StopAsync timeout | Runtime interop primitives used by adapters |
| AXG010 | ~10 | Guardrails analyzer recovering from FCS failures | Tooling is host role; rule does not apply |
| AXG010 | ~10 | specific exception converted to a value (JsonException, missing pidfd, temp-name retry) | Only flag catch-all handlers that ignore the exception |
| AXG010 | ~6 | observer/defect/annotation sink failure must not change the outcome | One `Report.safely` primitive |
| AXG011 | 25 | cleanup must finish after caller cancellation; roots without a parent | `Flow.uninterruptible` (the only `CancellationToken.None` in the code base) and a named root token |

`CancellationToken.None` is needed only to run cleanup after the caller's token has been cancelled. Effect libraries
call this an uninterruptible region. User code and adapters use `ensuring`/`acquireRelease`/`uninterruptible`.

Build order: (1) primitives, with benchmarks and torture; (2) narrow the rules as above; (3) the guide-not-block
model, and removal of the suppression lock machinery.
