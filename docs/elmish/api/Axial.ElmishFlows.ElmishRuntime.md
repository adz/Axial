# ElmishRuntime

`ElmishRuntime<'env>` owns the root scope for all commands in one Elmish program. Create one per program with
[`ElmishRuntime.start`](#Axial.ElmishFlows.ElmishRuntime.start), passing an environment and a `Post` function that
delivers callbacks on the host's dispatch thread.

## Construction

`ElmishRuntime.start options environment` builds a runtime from `ElmishRuntimeOptions` (`Post`, `OnDefect`,
`HistoryCapacity`, `Clock`, `LogSink`). `Clock = None` and `LogSink = None` keep the live runtime defaults; `Some clock`
installs that clock for commands, subscriptions and layer provisioning, and `Some sink` replaces the default log sink
there. When already on the dispatch thread, `Post` should run inline; otherwise it should post to it. Do
not call Elmish's dispatch directly from workers. Connect `OnDefect` to the host's crash reporting path; typed
failures are messages, but defects are not.

The runtime accepts any environment and needs no clock member. Keep the environment stable, and supply a
command's current dependencies with `Flow.localEnv`.

## Shutdown

- `runtime.StopAndWait timeout` interrupts commands and subscriptions, waits for their finalizers, and returns
  `false` on timeout; it is safe from a single-threaded UI context.
- `runtime.Stop()` is the asynchronous alternative.
