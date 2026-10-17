# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

## Integration follow-up

- `Reified.*` packages are now on NuGet (latest 0.8.2; `Directory.Build.props` pins `ReifiedVersion` 0.7.0). Restore and test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples/tests, and `examples/Axial.ReferenceApp` against package references.
- Move the cross-product reference application and host examples to a separate integration/examples repository after both release trains are public.

## Product work

- Flow-valued resource release and `Layer.fromFlow`. Release and layer construction are raw `Task`/`Async`
  callbacks, so cleanup cannot use `Ref`, `'env` services, or typed failures, and docs fall back to `lock`.
  - Release becomes `'value -> Flow<'env, 'none, unit>`: it runs uninterruptibly with the environment captured at
    acquisition, and a failure is a defect (as in ZIO). Change `Resource.create`, `Resource.ofAsync`,
    `Resource.finalizer`, `Resource.asyncFinalizer`, `Flow.scopeAcquireRelease`, `Flow.scopeFinalizer`, and
    `Flow.scopeAsyncFinalizer` (`src/Axial/Flow.fs`). Before 1.0, drop the task/async forms or keep them only as thin
    adapters.
  - Add `Layer.fromFlow`; make `Layer.acquireRelease` and `Layer.addFinalizer` take flow releases. `Layer.fromAsync`,
    `fromTask`, and `fromValueTask` (`src/Axial.Layers/Layer.fs`) hand the caller the raw `Scope` and expect a
    hand-built `Exit`; replace their uses in the layers guide, its tutorial, `Axial.PlatformService`, and the layers
    torture scenario.
  - Rewrite doc examples to use `Ref` instead of locks or `Interlocked`: `docs/09-scopes/_index.md` (connection log),
    `docs/08-concurrency-and-state/01-fibers.md` (lines ~163 and ~190), `04-deferred-semaphore.md`, `09-cache.md`,
    and `02-supervision.md` (poll counter).
  - Benchmark `ParallelCompositionBenchmarks` and scope close before and after; the empty-close fast path in
    `Platform.runFinalizers` must stay allocation-free.
- Flow-shaped process I/O and web handlers.
  - `Process.Input.read`, `Input.produce`, `Output.callback`, and the `Sink` target (`src/Axial.Process/Process.fs`)
    take `Async` callbacks with no environment. Add a `FlowStream<_, _, byte[]>` input and a flow output callback (or
    output into a `Queue`); decide how a process spec carries `'env`.
  - `AspNetCore.handleParsed` (`'model -> Task<IResult>`) and `GenHttp.handleParsed` (`'model -> ValueTask<IResponse>`)
    are unused by docs and examples: take a flow or remove them.
- Services return flows for asynchronous operations. Async members return `Flow<unit, 'error, 'value>`, with
  dependencies supplied at construction; `IProcess.Stream` already does this. Covers `IFileSystem` (`Task`) and
  `IHttp.Send` (`Async<Result<...>>`). Keep `IClock` as it is (the runtime schedules with it) and keep synchronous
  members synchronous (`UtcNow`, `ILog`, `IRandom`, `IGuid`). Update live implementations, fakes, and guides; let
  recording fakes in the services and dependencies guides use `Ref`.
- `FlowStream.fromCallback`: bridge .NET callbacks (events, `IObservable`, `ActivityListener`, runtime hooks) into a
  stream fed through a scope-owned queue, with the subscription released when the stream ends, like ZIO's
  `ZStream.async`. `Queue.tryOffer` is already synchronous. Use it to remove the locks in the telemetry guide
  (`ActivityListener`) and the supervision guide (`OnUnobservedDefect`, possibly as a defect stream). Consider an
  `Axial.Guardrails` rule for `lock` inside `flow { }`.
- Make hand-written recursive loops (`let rec loop () = flow { ...; return! loop () }`) run in constant memory on
  .NET. Each `return!` of a pending `ValueTask` must be awaited by the caller, so every iteration stays live until
  the loop ends (~420 B per suspending iteration, ~5 B per synchronous one; measured 2026-09-26). Library loops —
  `flow { while/for }`, stream consumers and skipping operators, `Flow.retry`/`repeat`/`supervise`,
  `STM.atomically`, `Queue.offerAll` — already use `Platform.loop`. Fixing user recursion needs the
  runner to trampoline binds itself; see `dev-docs/current-ideas/flow-as-data.md`. Until then, docs should steer
  long-running loops to `while`, streams, or schedules.
- Reassess remaining demand-driven Flow work in `LATER_TODO.md` against a concrete application before expanding the API.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters and retained reference application cross the product boundary.
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
