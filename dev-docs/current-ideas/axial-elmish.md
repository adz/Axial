# Axial.Elmish: a per-program runtime for Elmish apps

Status: accepted for 0.11 as `TASKS.md` T16; promote the settled rules into `PLAN.md` and delete this sketch when it
ships.

## Why

Two desktop apps built on Axial each wrote their own flow-to-Elmish bridge, and both bridges have bugs:

- **gitkay** (`~/projects/gitkay/main/src/Axial.Elmish`, read at `15ef587`): an `AxialElmishRuntime` that is only a
  `CancellationTokenSource` plus `App.start` hosts; `AxialLatestSlot` (11 module-level slots in `App.fs`); a static,
  process-global `CmdDiagnostics` `FiberRegistry`; `Cmd.OfFlow.ofFlow`/`ofFlowLatest`.
- **Logs Digger** (`~/projects/logdug`): a `FlowCmd` module, an `AppEnv.Post` to the UI thread, and a hand-built
  `Axial.App` root that hands long-lived services out through a `TaskCompletionSource`.

Problems found in those bridges (feedback report, 2026-10):

1. **Worker-thread dispatch corrupts Elmish (verified).** Elmish 5's loop uses an unlocked `RingBuffer` and a plain
   `mutable reentered`. Two threads dispatching 200,000 messages each processed messages more than once, and one run
   crashed with `NullReferenceException`. gitkay dispatches from the thread pool at the end of `Cmd.OfFlow.run`, from
   search worker callbacks, and from the C# debounce fiber.
2. **Stopping an Axial app on the UI thread deadlocks (verified).** `App.start` runs `Async.StartWithContinuations` on
   the caller's thread, so the synchronous prefix runs on the UI thread and the settle continuation is posted back
   through its `SynchronizationContext`. `Async.RunSynchronously(handle.Stop(), 5000)` on the UI thread never returns,
   and the timeout does not help. gitkay avoids it only by never awaiting cleanup at exit.
3. **Defects never reach the crash path (read).** gitkay's `dispatchOrNothing` raises `Cause.Die` inside a `task { }`
   whose result is ignored, so the defect becomes an unobserved task exception, not the top-level crash dialog.
4. **Latest-wins is module-level mutable state.** Each concern needs a hand-declared `AxialLatestSlot`, and slots
   outlive the program that uses them.
5. **Synchronous flow prefixes freeze the window.** gitkay works around it with `Task.Run` per command.
6. **Long-lived stream consumers are hand-built.** gitkay's C# `Debounce<'value>` (queue, mutable sink, supervising
   fiber, generation counter) and Logs Digger's `Hub` plus Elmish subscription are the same thing written twice; Logs
   Digger's `FlowStream.debounce` / `groupedWithin` / `switchMapFlow` pipeline is the shape that worked.
7. **Diagnostics are global.** gitkay's named-fiber registry (diagnostics window, hang dump, cancel by name) is the best
   diagnostics either app has, but it is a static, so two programs or two tests share it.

## Shape

A new package `Axial.Elmish` in this repository, referencing `Axial` and `Elmish` 5 only. It knows nothing about
Avalonia or Elmish.Avalonia.Glue; the host supplies UI delivery.

### Runtime

```fsharp
type ElmishRuntimeOptions =
    { /// Runs a delivery on the program's dispatch thread: inline when already there, posted otherwise.
      Post: (unit -> unit) -> unit
      /// Receives every defect from a command or subscription; the host wires it to its crash path.
      OnDefect: CommandDefect -> unit
      /// Settled-fiber history kept by the runtime's registry.
      HistoryCapacity: int }

/// A defect from a named command or subscription, with its cause rendered AOT-safely.
type CommandDefect = { Name: string; Error: exn option; Rendered: string }

type ElmishRuntime<'env> =
    member Environment: 'env
    member Registry: FiberRegistry          // per runtime, never static
    member Stop: unit -> Async<unit>        // interrupts every fiber and awaits finalizers
    member StopAndWait: timeout: TimeSpan -> bool   // safe on any thread, honours the timeout
    interface IDisposable

module ElmishRuntime =
    val start: ElmishRuntimeOptions -> 'env -> ElmishRuntime<'env>
    /// Builds the environment from a layer whose resources live until the runtime stops.
    val startLayer: ElmishRuntimeOptions -> Layer<'input, 'error, 'env> -> 'input -> Async<Result<ElmishRuntime<'env>, 'error>>
```

- The runtime owns **one root `App`** and its root scope. Commands, subscriptions and long-lived services are fibers
  or resources in that scope, so `Stop` interrupts all of them and awaits their (flow-valued, T1) finalizers.
- Every message reaches Elmish's `dispatch` **through `Post`**, never directly from a worker. This makes Axial.Elmish
  safe on any host even before Glue fixes its own dispatch (see "Outside this package").
- `startLayer` replaces Logs Digger's `TaskCompletionSource` handoff and covers the general "start a root, hand its
  services to the UI" pattern.

### Commands

Names follow Elmish's own `Cmd.OfAsync` vocabulary.

```fsharp
module Cmd.OfFlow =
    val either:  name: string -> ElmishRuntime<'env> -> Flow<'env, 'error, 'value> -> ('value -> 'msg) -> ('error -> 'msg) -> Cmd<'msg>
    val perform: name: string -> ElmishRuntime<'env> -> Flow<'env, Never, 'value> -> ('value -> 'msg) -> Cmd<'msg>
    val attempt: name: string -> ElmishRuntime<'env> -> Flow<'env, 'error, unit> -> ('error -> 'msg) -> Cmd<'msg>

    /// Latest-wins: starting a command with a key interrupts the fiber that key currently holds in this runtime.
    module Latest =
        val either:  key: string -> ElmishRuntime<'env> -> Flow<'env, 'error, 'value> -> ('value -> 'msg) -> ('error -> 'msg) -> Cmd<'msg>
        val perform: ...
        val attempt: ...
        val cancel:  key: string -> ElmishRuntime<'env> -> Cmd<'msg>
```

- Each command is forked as a **named fiber** in the runtime's registry, annotated with its name, and started off the
  dispatch thread so a synchronous prefix cannot freeze the UI.
- Exit mapping: success → `onSuccess`; `Cause.Fail` → `onError` (typed errors travel as data through messages);
  interruption → nothing; anything else (`Cause.Die`, compound causes) → `OnDefect`, never raised into an ignored task.
- Latest-wins keys live in the runtime, not in module-level slots; per-item keys (one per selected file) are just
  `$"context:{file}"`. `AxialLatestSlot` and `AxialLatestSlotRegistry` disappear.
- Cancel by name and the hang dump come from `Registry` (`InterruptByName`, `DumpAt`).

### Subscriptions and inputs

```fsharp
module Sub.OfFlowStream =
    /// Runs a stream as a fiber in the runtime's scope while the subscription is active; Elmish 5 disposes it when
    /// the id leaves the subscription list, which interrupts the stream and runs its finalizers.
    val subscribe: id: SubId -> ElmishRuntime<'env> -> FlowStream<'env, 'error, 'msg> -> ('error -> 'msg) -> Sub<'msg>

type Inbox<'value>
module Inbox =
    /// A bounded, runtime-owned queue the UI writes to synchronously (Queue.tryOffer; full means dropped and counted).
    val create: capacity: int -> ElmishRuntime<'env> -> Inbox<'value>
    val offer: 'value -> Inbox<'value> -> Cmd<'msg>
    val stream: Inbox<'value> -> FlowStream<'env, 'error, 'value>
```

The search pipeline both apps needed is then data, not classes:

```fsharp
let searchSub runtime inbox =
    Inbox.stream inbox
    |> FlowStream.debounce (TimeSpan.FromMilliseconds 300.0)
    |> FlowStream.switchMapFlow runSearch
    |> FlowStream.groupedWithin 500 (TimeSpan.FromMilliseconds 200.0)
    |> FlowStream.map SearchResults
    |> Sub.OfFlowStream.subscribe [ "search" ] runtime SearchFailed
```

This replaces gitkay's `Debounce<'value>`, the C# `OnSearchDebounced`, the locked buffer with a hand-written 200 ms
throttle, and `AxialElmishRuntime.Host`.

### Testing

`ElmishRuntime.test env` gives a runtime whose `Post` enqueues onto a single-threaded queue the test drains, with
`OnDefect` collected into a list. Helpers run a `Cmd` and return the dispatched messages, and `settle timeout` waits
until no command fiber is running. Combined with `ManualClock` in `env`, debounce and timeout tests are deterministic
(gitkay's latest commit exists only to stop a debounce test racing the manual clock).

## Prerequisites in core Axial

- **`App.start` off the caller's context** (problem 2): start the root on the thread pool, settle without capturing
  the caller's `SynchronizationContext`, and add `AppHandle.StopAndWait(timeout)` that is safe on any thread and honours
  its timeout. Committed test: stop from a single-threaded `SynchronizationContext` completes within the timeout.
  Fable keeps today's behaviour (one thread).
- **T1** (flow-valued releases) so runtime-owned resources clean up with `Ref` and `'env`.
- **T4** (`FlowStream.fromCallback`) is useful for host event sources but not required.

## Required tests

- Many fibers dispatching concurrently: every message delivered exactly once, all through `Post`, never overlapping.
- `StopAndWait` from a UI-like single-threaded context returns, runs finalizers, and honours its timeout.
- Latest-wins interrupts the stale fiber and the stale result is never dispatched.
- A defect reaches `OnDefect`; an interruption dispatches nothing; a typed failure dispatches `onError`.
- Removing a subscription id interrupts its stream and releases a scoped resource.
- A synchronous flow prefix does not run on the dispatch thread.
- Error rendering stays AOT-safe (no `%A`, no generated union `ToString`).

## Done when

- gitkay deletes `src/Axial.Elmish`, its 11 slots become keys, `Debounce<'value>` becomes an inbox and a subscription,
  and `CmdDiagnostics` reads the runtime's registry; shutdown awaits cleanup.
- Logs Digger replaces `FlowCmd`, `AppEnv.Post`, and its hand-built root with the runtime.
- A user guide under `docs/` covers commands, latest-wins, subscriptions, inboxes, shutdown, and testing.

## Open questions

- **Fable.** Elmish runs on Fable and Axial core is Fable compatible; keeping Axial.Elmish Fable-compatible needs a
  non-`Task.Run` way to leave the dispatch thread (a scheduler hop in the runtime). Recommended: yes, if the hop is
  cheap.
- **Latest-wins key type.** `string` keys are simple and show up in diagnostics; a generic `'key` is safer but makes the
  runtime generic in it. Recommended: `string`.
- **Where the dispatch-thread `Post` for Avalonia lives.** Recommended: Glue ships it (it already owns the
  dispatcher); Axial.Elmish only takes the function. This also covers the `LATER_TODO.md` UI-dispatcher item.

## Outside this package

These came from the same report and belong to Elmish.Avalonia.Glue, not Axial:

- Start Elmish with `Program.runWithDispatch` and a `syncDispatch` that runs inline on the UI thread and posts
  otherwise; correct the background-work guide that calls worker dispatch safe.
- Make `startAndBind` deliver inline when already on the UI thread (queued delivery pushes stale text into two-way
  `TextBox` bindings).
- A notifying F# projection base, `SyncWith` over `seq`/`list`, a clear duplicate-key error, an all-F# UI page, and a
  headless test helper.
