# T7 reassessment: FsLiveDocs

Read at FsLiveDocs `ddca61f` (Axial 0.16.0), 2026-11-27. Labels as in `gitkay.md`.

## 1. Shape

A CLI and static-site builder that runs F# documentation examples, audits API docs, and serves a watch-mode
preview. It uses `Axial`, `Axial.FileSystem`, `Axial.Process`, `Axial.HttpClient`, `Axial.Console` and
`Axial.Configuration`. It does not reference `Axial.Guardrails`. Flow use is partial: about 53 `flow { }` blocks
against 19 `async { }`, with 14 `Async.RunSynchronously` calls, mostly around FSharp.Compiler.Service, which returns
`Async`.

## 2. Workarounds

Most of these are adoption gaps where the Axial feature already exists:

| Where | What | Axial feature that covers it |
|---|---|---|
| `src/FsLiveDocs.Core/ReleaseCapsule.fs:959-983` | A download retry loop: raw `HttpClient`, `.GetAwaiter().GetResult()`, `Thread.Sleep(attempt * 2s)`, and a transient-status filter. | `Axial.HttpClient` plus `Flow.retry` with `Schedule.exponential` and a typed predicate |
| `src/FsLiveDocs.Runner/FsiTranscriptRunner.fs:131-147` | `SemaphoreSlim` bound plus `Async.Parallel` plus `Async.RunSynchronously` over batches. | `Flow.traversePar (Parallelism.bounded n)` |
| `src/FsLiveDocs.Cli/CommandLine.fs:195-235` | Watch mode: raw `FileSystemWatcher`, a `MailboxProcessor` debounce, and a manual restart on `Error`. | `Files.watch` plus `FlowStream.debounce`, as gitkay and LogDug already do |
| `src/FsLiveDocs.Cli/Commands/Actions.fs:592-659` | A spinner: `Task.Run` plus `Thread.Sleep 80` plus a `CancellationTokenSource`, with a `lock`-guarded status. | new: console progress/status (§5) |
| `src/FsLiveDocs.Cli/Timing.fs` | Phase timing with a global `lock`, `Stopwatch` and `DateTimeOffset.UtcNow`. | §14 tracing, or `Axial.Telemetry` spans with a local sink; check whether these already fit |
| `ProjectResolver.fs:160,195`, `TranscriptHostClient.fs:118`, `SemanticExtractor.fs:18`, `DocumentationCompiler.fs:160`, `SourceParameters.fs:54` | Process-lifetime `ConcurrentDictionary<_, Lazy<_>>` memoisation of expensive builds and FCS checks. | `Cache.make` once the callers are flows; until then the same "memo outside a Flow" gap as gitkay |
| `SourceParameters.fs:99`, `DocAnalysis.fs:565,583`, `Verification.fs:75,79`, `PackageExtraction.fs:323,356`, `Actions.fs:380,389`, `Program.fs:193` | `Async.RunSynchronously` on FCS and symbol extraction from synchronous code. | `Flow.fromAsync` once the callers are flows |

## 3. Needed items

- **§14 tracing / tagged measurements** (read): `Timing.fs` is a hand-written phase tracer. Check `Axial.Telemetry`
  first, because a local span sink may already be enough.
- **§12 text encoding pipelines**: no evidence. Transcript I/O goes through `Axial.Process`.

## 4. Not needed (no evidence)

- §10, §11 (no STM), §13.
- §14 latch/barrier/mutex: the one `SemaphoreSlim` is a parallelism bound.
- §14 accessor codegen: 4 environment interface implementations.

## 5. New gaps

- **Console progress/status** (read). A spinner or status line that runs as a fiber beside the work, stops when the
  work's scope ends, and writes through `IConsole`. It would replace `Actions.fs:592-659`. gitkay's progress bar is a
  UI concern, so FsLiveDocs is the only consumer.
- **Guardrails adoption** (observed). FsLiveDocs is the one Axial app without Guardrails, which is why the raw retries
  and threads above survive. Turning it on in Default mode would list them as AXG001 findings. That is a decision for
  FsLiveDocs, not an Axial change.

## 6. Bugs found in Axial

- **T42** (observed, already queued): an MSBuild node holding inherited pipes hung a FsLiveDocs CI run in 0.14.6.
