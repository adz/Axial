# T7 reassessment: LogDug

Read at LogDug 0.5.2 (Axial 0.16.0), 2026-11-27. Labels as in `gitkay.md`.

## 1. Shape

An Avalonia desktop log viewer written in F#. It browses a folder, tails and decodes log files (including
archives), and searches them. It uses `Axial`, `Axial.ElmishFlows`, `Axial.FileSystem`, `Axial.Configuration`,
`Axial.Hosting` and `Axial.Guardrails`. It has no capability claims and no allowances, and its Guardrails baseline
is empty.

## 2. Workarounds

| Where | What | Maps to |
|---|---|---|
| `src/LogDug.Core/Runtime.fs:88-99` | `ElmishRuntime.startLayer ... \|> Flow.fromAsyncResult \|> Flow.run () \|> Exit.getOrThrow`, the host's one synchronous wait. | new: synchronous host start (see `gitkay.md` §5) |
| `src/LogDug/Desktop.fs:67` | Clipboard write as fire-and-forget `Async.Start(... SetTextAsync ...)`. The host project's role allows it. | new, minor: desktop services (clipboard, folder picker) as capabilities with fakes |
| `src/LogDug/Views/MainWindow.axaml.fs:149` | Folder picker in a `task { }`, result ignored. | same as above |
| `src/LogDug/Views/MainWindow.axaml.fs:63,90,105` | `Dispatcher.UIThread.Post` for scroll and focus. | none: view code, correct as is |

## 3. Needed items

- **§12 compression pipelines** (read, weak). `Files.fs:245` decodes whole archives with
  `Flow.attemptBlocking (fun _ -> Archive.decode kind bytes)`. A streaming decompression stage would avoid holding the
  whole archive in memory. That matters only for large archives, and no user has asked.

## 4. Not needed (no evidence)

- §10, §11 (no STM), §13, §14.
- §12: the async-callback constructor (search events use `Hub` and `FlowStream.fromHub`), `runExactlyOne`, channels,
  stream retry. Its `Files.watch` streams have no restart logic.
- §14 accessor codegen: 5 environment interface implementations.

## 5. New gaps

- The synchronous host start (shared with gitkay).
- Desktop services, minor (read). Clipboard and the folder picker are the only effects left outside Axial. Both live
  in view code, so this is worth doing only together with an Avalonia package or the §7a Avalonia example.

## 6. Bugs found in Axial

None. The `Async.RunSynchronously` claim that LogDug carried is gone in 0.5.2.
