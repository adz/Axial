# T7 reassessment: gitkay

Read at gitkay `5abe7b5` (Axial 0.16.0), 2026-11-27. Labels: verified (ran something), observed (hit while
building), read (read the code), guess.

## 1. Shape

A desktop Git client, built with Avalonia. The UI is C# code-behind over an F# core, and the core is an Elmish
program. It uses `Axial`, `Axial.ElmishFlows`, `Axial.FileSystem`, `Axial.Process`, `Axial.HttpClient`,
`Axial.Console` and `Axial.Guardrails`. LibGit2Sharp handles repository reads.

## 2. Workarounds

| Where | What | Maps to |
|---|---|---|
| `src/GitKay.UI/GitOperationWindow.cs` `GitRunner` | Runs `git push/pull/fetch` with a hand-written `Process` wrapper: two stdout/stderr pumps, a cancellation registration that kills the process tree, `GIT_TERMINAL_PROMPT=0`, and lines rewritten on `\r` so progress updates in place. | Fixed in 0.17.0: line framing ends at a bare CR and `ProcessOutput.Ending` marks redraws. Still wants the C#-callable run (next row). |
| `src/GitKay.Core/Host.fs` `UiFlow`, `UiLaunch` | C# code-behind cannot run a `Flow` directly, so the core wraps `Cmd.OfFlow.either` in an `Action<Result>` callback for it. | new: a C#-facing runner on `ElmishRuntime` (§5) |
| `src/GitKay.Core/Host.fs:49` | `ElmishRuntime.startLayer ... \|> Async.RunSynchronously` at startup. The file is allowed through the `host` role. LogDug writes the same wait as `Flow.fromAsyncResult \|> Flow.run \|> Exit.getOrThrow`. | new: a synchronous host start (§5) |
| `src/GitKay.Core/Git/Search.fs:516-545` | A search worker pool built from a `ConcurrentQueue` of 32-commit chunks, an `Interlocked` progress counter, and `forkNamed` plus a join loop, with one object reader per worker. | Covered since 0.16.0 by `Flow.forEachParUsing` over the chunk list, with `Ref` for the count. Adoption, not a gap. |
| `src/GitKay.Core/Processors.fs` + `AxialCapability="processors"` | Sizes workers from `Environment.ProcessorCount`. | Fixed in 0.17.0 by `Parallelism.count`. |
| `src/GitKay.Core/CmdDiagnostics.fs:63` | An unobserved defect has no time, so it is stamped with `DateTimeOffset.UtcNow` under an allowance. | Fixed in 0.17.0 by `UnobservedDefect.At`. |
| `src/GitKay.Core/App.fs` `WatchFailures`/`WatchRestarts`, `maxWatchFailures = 3` | The model counts watcher failures and bumps the subscription id to restart a failed `Files.watch` stream, at most 3 times in a row. | §12: stream `retry` with a `Schedule` |
| `src/GitKay.Core/Markdown.fs:658-662` | Module-level `ConcurrentDictionary` memo caches used from pure render calls, cleared wholesale once they reach 128 entries. | Not an Axial gap (decided in T7): memoising a pure function is not an effect; any LRU serves. |
| `src/GitKay.Core/GitService.fs:1133` `commitImageCache` | A process-wide cache of blobs read from the repository: an effectful lookup, keyed by repository path, never evicted, surviving repository switches and shared across tests. | Adoption: a `Cache.makeBounded` built in the `GitService` layer and held in `GitEnv`, as gitkay already does for other lookups. |
| `src/GitKay.Core/Timing.fs` | A process-wide `Clock.live` read outside any flow, so C# can take timing marks. | none needed; a sanctioned live read |

## 3. Needed items

- **§12 stream retry/timeout.** `subscriptions` in `App.fs` would become
  `WorkingTree.changes ... |> FlowStream.retry (Schedule.recurs 3 ...)`, and `WatchFailures`/`WatchRestarts` would
  leave the model. This needs a schedule that resets after a success ("no change seen in between").

## 4. Not needed (no evidence)

- §10 runtime flags.
- §11 STM: no use at all.
- §12 async-callback constructor (search progress already uses `Inbox`), `runExactlyOne`, channels, encoding
  pipelines.
- §13 differ/patch.
- §14 latch/barrier/mutex/concurrent map: the only concurrent collections are memo caches and the worker queue
  covered above.
- §14 accessor codegen: there are 4 hand-written environment interface implementations, which is not a burden.

## 5. New gaps

- **Carriage-return progress lines** (read). **Fixed in 0.17.0.** `Axial.Process` split streamed output only on
  `\n`, so `git --progress` redraws arrived as one line at the end. Line framing now ends a line at a bare CR, as
  `StreamReader.ReadLine` does, and `ProcessOutput.Ending` marks each `Cr` line, so a display can replace it.
- **C#-callable Flow running** (read). A UI written in C# needs `runtime.Run(flow, onResult)` or a `Task<Result>`
  returning call that is still a named, interruptible command fiber. `UiFlow.run` is that helper, written in the app.
- **Synchronous host start** (read). Both desktop apps block on `ElmishRuntime.startLayer` before the UI starts.
  Wanted: one sanctioned call, such as `ElmishRuntime.startLayerSync` or a documented host-edge recipe, instead of
  two spellings of the same bridge.
- **Bounded memo for pure functions** (read). Decided not an Axial gap: memoising a pure function is not an
  effect (compare Elm's `Html.lazy`), and the clear-all-at-128 eviction is gitkay's to improve.

## 6. Bugs found in Axial

None from this read. The push fix in gitkay 0.17.2 was a gitkay bug.
