# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

## Integration follow-up

- `Reified.*` packages are now on NuGet (latest 0.8.2; `Directory.Build.props` pins `ReifiedVersion` 0.7.0). Restore and test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples/tests, and `examples/Axial.ReferenceApp` against package references.
- Move the cross-product reference application and host examples to a separate integration/examples repository after both release trains are public.

## Concurrency foundations after Queue and Hub

Follow-up to the Queue/Hub/Schedule review. Work in order; each step is committed and validated on its own.

3. Give every forked fiber its own child scope, closed when the fiber settles, so a fiber's acquisitions (including
   hub subscriptions) end with it. Add `Flow.forkGraceful stop grace`: on scope close, run `stop`, wait up to
   `grace` for the fiber to finish, then interrupt it, so a consumer can drain a shut-down queue on application stop.
4. Move time into the runtime context as an internal replaceable time source (monotonic now plus delays) used by
   `Flow.Runtime.sleep`, timeouts, retry delays, `Schedule`, and the new timed stream operators; add a virtual time
   source for deterministic tests.
5. Add `SubscriptionRef<'a>`: a `Ref` whose `changes` stream emits the current value and then every update, with no
   gap or duplicate, for latest-value consumers and late joiners.
6. Connect streams and the concurrency types: `FlowStream.fromHub`, `runIntoQueue`, `runIntoHub`, `mergePar`,
   `buffer`, `fromSchedule`, `groupedWithin`, and `throttleLatest`.
7. Report queue and subscription depth, drops, and evictions through the runtime telemetry sink.
8. Hub fixes and gaps: register the subscription finalizer before adding it to the hub; keep sliding queues within
   capacity when a cancelled taker gives a value back; add `Hub.tryPublish`, `Hub.makeScoped`, `Hub.awaitShutdown`,
   and `PublishResult.Evicted`; document that a full `BackPressure` subscriber delays every later subscriber and that
   an interrupted `publish` is not safe to retry.
9. Tests: a cancelled taker hands its value to the next suspended taker; the mixed-subscriber test asserts the
   publisher never suspended; deterministic `fixedRate` tests on virtual time.
10. Housekeeping: fix `LATER_TODO.md`, record decisions, update `docs/llms.txt`, delete `dev-docs/queues_and_hubs.md`.
11. Add a docs page with a torture test that exercises every guarantee above under interruption, shutdown, overflow,
    and concurrent publishers.
12. Run the full validation list, including `run-aot-probe.sh` and `dotnet livedocs test --warn-as-error`.

## Product work

- Make hand-written recursive loops (`let rec loop () = flow { ...; return! loop () }`) run in constant memory on
  .NET. Each `return!` of a pending `ValueTask` must be awaited by the caller, so every iteration stays live until
  the loop ends (~420 B per suspending iteration, ~5 B per synchronous one; measured 2026-09-26). Library loops —
  `flow { while/for }`, stream consumers and skipping operators, `Schedule.retry`/`repeat`, `Flow.Runtime.retry`/
  `supervise`, `STM.atomically`, `Queue.offerAll` — already use `Platform.loop`. Fixing user recursion needs the
  runner to trampoline binds itself; see `dev-docs/current-ideas/flow-as-data.md`. Until then, docs should steer
  long-running loops to `while`, streams, or schedules.
- Reassess remaining demand-driven Flow work in `LATER_TODO.md` against a concrete application before expanding the API.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters and retained reference application cross the product boundary.
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
