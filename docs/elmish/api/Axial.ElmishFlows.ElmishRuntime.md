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

## Services

`runtime |> ElmishRuntime.startService name service` starts a `Flow<'env, Never, unit>` as a named fiber in the
runtime's root scope, outside the Elmish program, so `init` and `update` never start or restart it. It returns
`Result<unit, ServiceStartError>`: `Error ServiceStartError.DuplicateName` while a service with that name is still
running, and `Error ServiceStartError.RuntimeStopping` once the runtime is stopping. Stopping the runtime interrupts the
service. A defect goes to `OnDefect` under the service name, and so does a service that completes while the runtime is
still running, because a service is expected to run until the runtime stops.

Unlike `Cmd.OfFlow.start`, which the program runs from `init` or `update` and which is expected to finish, a service
is started by the host, one per name, and reports whether it started.

## Shutdown

- `runtime.StopAndWait timeout` interrupts commands and subscriptions, waits for their finalizers, and returns
  `false` on timeout; it is safe from a single-threaded UI context.
- `runtime.Stop()` is the asynchronous alternative.
