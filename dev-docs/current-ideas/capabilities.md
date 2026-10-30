# Capabilities, part 1 (T25a): Platform in the runtime

Status: design for owner review. No code, TOML or lock changes until approved. Covers T25a only; capability declarations
(`ICapability`, the manifest) are T25b and the Guardrails rules for them are T25c.

Inputs: `~/projects/scratch/axial-coord/pi/capabilities-spec.md` (section 10 supersedes 1 and 8),
`dev-docs/current-ideas/platform-env-class.md`, `dev-docs/current-ideas/richer-effects.md`, and the code as of `73b71c33f`.
Owner decisions are restated as decisions; everything else is a recommendation with its alternative.

## 1. Problem

Four things every Axial application and test carries by hand:

1. **Env boilerplate.** `Flow.sleep`, `timeout`, `retry`, `repeat`, `supervise`, `fork`, `Cache.make`, streams (`fromSchedule`,
   `groupedWithin`, `throttle`) all require `'env :> IHasClock`. Applications add `IHasLog`, `IHasRandom`, `IHasGuid` for their own
   code. About 70 files in `src`, `tests`, `docs` and `examples` mention `IHasClock`/`IHasRandom`/`IHasGuid`/`IHasLog`/`IHasSecureRandom`.
   `platform-env-class.md` records the cost: roughly thirty repeated lines per environment, and a new platform service is a breaking change.
2. **Tests that guess.** `runOnManualTime` (tests/Axial.Tests/TestSupport.fs) decides the flow is idle after about 15 ms of silence plus
   thread-pool counters. That is quiescence counting by wall clock.
3. **No safe default for logging.** A flow that wants to log needs `IHasLog` and a service. Library code cannot log without forcing its
   callers' envs to grow.
4. **A guardrail with no target.** `ISecureRandom` was added only so the `RandomNumberGenerator` rule has something to point at. Nothing uses it.

## 2. Decisions (owner, 2026-10-25/26/30)

- Platform is closed: **Clock, Guid, Random**. These are values flows *read*, so a test must be able to control them, and each has one correct fake.
- **Log and telemetry are write-only observation slots**, not Platform. Public interfaces `ILogSink` and `ITelemetry`, installed with
  `Flow.withLogSink` / `Flow.withTelemetry`, inherited by forked fibers like `withFiberObserver`.
- Default log sink: Console, Info and above, on **stderr** (`Axial.Tool` writes JSON on stdout). Benchmarks and tests install the no-op or recording sink explicitly.
- The capability contract is `Live` + `Fake` + `Replaces` (T25b). "Null" is only the runtime default when nothing is installed.
- No general "register any service in the runtime context" API. Authority (files, HTTP, process, secrets) stays an explicit capability in `'env`.
- Test time is explicit stepping, never quiescence counting.
- `ISecureRandom` is removed.

## 3. The shape

### 3.1 Where the types live

`IClock` is already in `Axial` (Outcome.fs). `IRandom`, `IGuid`, `ILog` live in `Axial.PlatformService`, which depends on `Axial`; the runtime
context is internal to `Axial`. So **`IRandom` and `IGuid` move into `Axial`** (same names; `Axial.PlatformService` keeps the `Random`, `Guid`,
`Clock`, `Log` modules, so call sites only change their `open`s). `ILog` is replaced by `ILogSink` in `Axial`. `LogLevel` is already in `Axial`.

```fsharp
namespace Axial

// IClock: unchanged. IRandom, IGuid: moved from Axial.PlatformService, unchanged members.

/// One log record. Annotations are the fiber's current annotations (Flow.annotate), so a sink sees trace_id etc. without the caller threading them.
type LogEntry =
    { Level: LogLevel
      Message: string
      Error: exn option
      Annotations: Map<string, string> }

/// Write-only. Anyone may implement one. Must not throw; a throwing sink is guarded, like fiber observers.
type ILogSink =
    abstract Write: LogEntry -> unit

/// Write-only telemetry. The existing runtime slots (annotation sink, attribute sink, ambient tracer) behind one public interface.
type ITelemetry =
    abstract Annotate: name: string * value: string -> unit
    abstract Attribute: Axial.Telemetry.Attribute -> unit
```

### 3.2 The runtime context

`RuntimeContext` (Core.fs) already carries scope, observer, annotation and telemetry sinks, tracer and tokens. Add two fields:

```fsharp
type internal PlatformServices = { Clock: IClock; Random: IRandom; Guid: IGuid }

type internal RuntimeContext =
    { ...existing...
      Platform: PlatformServices   // one field: copies of the context stay the same size apart from one pointer
      LogSink: ILogSink }
```

`RuntimeContext.create` fills in `Platform.live` and `LogSink.console` (Info, stderr). Forks, branches and disconnects already copy the parent
context with `{ runtime with ... }`, so they inherit both fields with no new code. `Telemetry` installs map onto the existing annotation/telemetry sinks.

### 3.3 Installing and overriding

```fsharp
module Flow =
    val withClock    : IClock    -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withRandom   : IRandom   -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withGuid     : IGuid     -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withLogSink  : ILogSink  -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withTelemetry: ITelemetry-> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
```

Each applies to the flow it wraps and everything that flow forks, and not outside it (the same rule and the same implementation shape as
`withFiberObserver`). A user never builds a `PlatformServices` record. The `with*` functions compose: telemetry and log sinks replace, they do not
tee, except that `Axial.Telemetry` composes through the existing `withComposed*` helpers.

### 3.4 Flows that read the services

No `IHasClock`:

```fsharp
Flow.sleep   : TimeSpan -> Flow<'env,'error,unit>
Flow.timeout, timeoutToOk, timeoutWith, retry, repeat, supervise, FlowStream.fromSchedule, groupedWithin, throttle, Cache.make...

Clock.now / Clock.elapsed / Clock.timed   : Flow<'env,'error,_>          // module in Axial.PlatformService, constraint removed
Random.next / nextMax / nextInt / nextDouble / nextBytes / bytes
Guid.newGuid
Log.info / warning / error / ... : string -> Flow<'env,'error,unit>      // no IHasLog
```

Mechanism, for example:

```fsharp
let sleep delay =
    Flow(fun _ ct ->
        let clock = RuntimeState.current().Platform.Clock
        Platform.timeOfClock clock |> fun time -> time.Sleep(delay, ct))
```

`Flow.run`, `runSync`, `runAsync` and `StartAsTask` keep their signatures and use the live defaults. Because `Flow.sleep` still returns a cold
`Flow`, nothing about laziness changes.

`Schedule<'env,'input,'output>` keeps its `'env` parameter: schedules can still read an environment. Only the clock requirement leaves it.

### 3.5 Test entry points

```fsharp
/// Everything a test controls. Replaces TestRuntime. Not an env: nothing here is looked up through 'env.
type TestPlatform =
    member Clock: ManualClock                  // epoch start; moves only when advanced
    member Log: TestLog                        // recording ILogSink
    member Random: IRandom with get, set       // Random.seeded 0 by default
    member Guid: IGuid with get, set           // Guid.sequence by default
    static member create: unit -> TestPlatform

module Flow =
    val runTestWith : TestPlatform -> 'env -> Flow<'env,'error,'value> -> Exit<'value,'error>
    val runTest     : 'env -> Flow<'env,'error,'value> -> Exit<'value,'error>   // runTestWith (TestPlatform.create ()) ...
```

`runTest` is `runSync` with the test platform installed: clock at the Unix epoch, seeded random, sequential guids, recording log. The
per-service overrides are the same `with*` functions, so a test can swap one service without a `TestPlatform`:
`flow |> Flow.withRandom (Random.seeded 42) |> Flow.runTest env`.

`Clock.fixed t` is a `ManualClock` that is never advanced. Its `Sleep` waits until someone advances it, so a flow that sleeps under a fixed clock
and nobody advances it hangs, deliberately (see 3.6).

### 3.6 Test clock: explicit stepping

Like elm-program-test `advanceTime` and ZIO `TestClock.adjust`: the test says when time moves, and waits on signals (`Deferred`, latch), never on sleeps.
`ManualClock` already has `AdvanceBy`, `AdvanceToNextDeadline` and `Sleepers`; we keep it and add flows that drive it from inside a test:

```fsharp
module TestClock =
    val advance : TimeSpan -> Flow<'env,'error,unit>    // runs due sleepers' continuations before returning, as ManualClock.AdvanceBy does
    val sleepers: Flow<'env,'error,int>                 // for asserting "the retry is waiting"
    // both die with a clear message if the current clock is not a ManualClock
```

A test that exercises a timed flow forks it, advances, then joins:

```fsharp
let! fiber = Flow.fork (Flow.retry policy flaky)
do! TestClock.advance (TimeSpan.FromSeconds 1.)
let! exit = Fiber.awaitExit fiber
```

**There is no automatic advance and no idle detection.** A test that waits on time nobody advances hangs; the fiber watchdog dump (T24) names
the sleepers it is blocked on. `runOnManualTime` is deleted. Its callers (WorkflowSchedulingTests, WorkflowStreamTests, TestSupport) move to explicit
stepping. Where a test is fully deterministic and only needs "all sleeps skipped", a documented helper `TestClock.advanceAll` (loop
`AdvanceToNextDeadline` while `Sleepers > 0`, called by the test, never by the runtime) covers it. `TestRuntime.Settle` in ElmishFlows stays: its own queue is a fact.

### 3.7 Log and telemetry

```fsharp
module Log =
    val info : string -> Flow<'env,'error,unit>             // and trace, debug, warning, error, critical
    val infoExn: exn -> string -> ...                        // errorExn, criticalExn, logException as today
    val live      : ILogSink    // Console, Info and above, stderr; renders "level: message" and the exception
    val none      : ILogSink
    val memory    : unit -> TestLog                          // "Memory" is just the name of Log's Fake: a recording sink
    val fromSink  : (LogEntry -> unit) -> ILogSink
```

`Log.info` always works because a default is installed. The sink is read once per call from the runtime context; the entry is built only when the
sink's level gate passes (see open question Q3).

Telemetry stays an observation slot. Today the slots are internal (`Telemetry.withSink` is hidden); `ITelemetry` makes them public and
`Flow.withTelemetry` composes them. The `Activity`/`ActivitySource` bridge lives in **`Axial.Telemetry`** (the only package that already boxes the
tracer), not in `Axial`, which keeps no dependency on `System.Diagnostics.DiagnosticSource`. The bridge is `Telemetry.withActivitySource source`. A
bridge from `ILogSink` to `Microsoft.Extensions.Logging` goes in `Axial.Hosting`, next to the existing logger-backed fiber observer.

## 4. What gets removed (the breaking change)

| Removed | Replacement |
|---|---|
| `IHasClock`, `IHasLog`, `IHasRandom`, `IHasGuid` | Runtime context; delete the field and the interface line from your env |
| `ClockEnvironment` | Not needed: `Flow.runTest ()` or any env |
| `ISecureRandom`, `IHasSecureRandom`, `SecureRandom` module | Nothing. Use `System.Security.Cryptography` inside a capability |
| `ILog`, `IHasLog`, `Log.service` | `ILogSink`, `Flow.withLogSink`, `Log.info` |
| `BaseRuntime.Clock/Log/Random/SecureRandom/Guid`, `servicesFromServiceProvider` for those | `IEnvironmentVariables` remains an env service (it reads external state with authority); see Q5 |
| `TestRuntime` env members | `TestPlatform` |
| `runOnManualTime` | Explicit stepping |
| `platform-env-class.md` sketch | Withdrawn (section 8) |

`ISecureRandom` removal is unreleased, so it costs no user. Its Guardrails rule (`RandomNumberGenerator`) keeps firing but now says: use
`System.Security.Cryptography` inside capability code. Files touched: `Services.fs`, `Operations.fs`, `Platform.fs`, `EffectCatalog.fs`,
`SecureRandomTests` in PlatformService and Fable tests, `RuntimeFoundationTests`, `GuardrailConfigTests`, docs `03-random-and-guid.md`, 0.12 notes.

Callers outside a Flow's dynamic extent need an explicit clock: `Axial.ElmishFlows` (`ElmishRuntime<'env when 'env :> IHasClock>`), `Axial.Process`
(clock in its own env type), `Axial.Hosting` watchdogs (`IHasClock`/`IHasLog`), `Cache` (reads the clock when created, inside a flow, so it can capture the
runtime one). Recommendation: `ElmishRuntimeOptions` gets a `Clock: IClock` defaulting to live, and `Elmish.test` takes a `ManualClock`; Hosting and Process
read the runtime context from inside their flows.

## 5. Visibility tradeoff

Reading the clock no longer shows in the Flow type. Before, `'env :> IHasClock` told a reader that a function is time-sensitive. After, it does not. We
accept this because the types protected nothing that tests now cannot (every read is controllable by `with*`) and the constraint was the main source of env
boilerplate. Mitigations:

1. **Guardrails** already bans the raw ambient APIs (`DateTime.UtcNow`, `Stopwatch`, `Guid.NewGuid`, `Random`; AXG001). Its messages change from "use IClock via your env" to "use `Clock.now`; the runtime provides it".
2. `axial capabilities` (T25b) lists runtime services with their live and fake.
3. **Optional trace mode** for tests: `TestPlatform.trace: bool` (or `Flow.withPlatformTrace`) records every Clock/Random/Guid read with the fiber id, so "what did this flow read?" is answerable from the test, not from the type.
4. Anything with authority stays in `'env` and in the type; only values that cannot do harm and have one correct fake move out.

## 6. Invariant tests (written first, they must fail before implementation)

1. A flow using `Flow.sleep`/`timeout`/`retry` runs under an env with no clock (e.g. `unit`).
2. `runTest` is deterministic: the same flow run twice produces identical clock reads, random values and guids.
3. `withClock` applies to a fiber forked in its scope (including `fork` under `race`/`zipPar` branches and `Flow.disconnect`) and does not apply outside it.
4. `withRandom` and `withGuid` follow the same rule; `withLogSink` too.
5. Explicit stepping: a `sleep 5s` fiber does not complete on `advance 4s` and completes on `advance 1s`; no test takes a wall-clock sleep.
6. Missing `advance` hangs (kept as a documented non-test, asserted by a watchdog-based test that the stall dump names the sleeper) and `TestClock.advance` on a live clock dies with the message.
7. The default log sink writes Info and above to stderr and nothing to stdout; a recording sink sees annotations.
8. A throwing sink or observer cannot change a flow's outcome.
9. Fable: the same clock/random/guid override semantics after an `await`, including inside `Flow.race` branches.
10. Guardrails: `File.ReadAllText`-style rules unchanged; `RandomNumberGenerator` message updated; `DateTime.UtcNow` message names `Clock.now`.

## 7. Benchmarks (quiet machine only)

The runtime context lives in an `AsyncLocal` on .NET, so every clock read becomes an `AsyncLocal.Value` read where it was a field read on `'env`.

| Benchmark | Question | Pass |
|---|---|---|
| sleep/timeout loop on a manual clock, env read (today) vs runtime read | read cost on the hot path | within noise; allocation identical |
| FiberCost, Race, Pingpong/Threadring | does adding two `RuntimeContext` fields (copied on every fork/branch) change allocation? | allocation within one pointer; time within noise |
| `Log.info` with a no-op sink and with a level-gated sink | cost of a disabled log call | zero allocation when gated off |
| a tiny `runSync` loop | no regression in the common path | within noise |

If the `AsyncLocal` read is measurable, the fallback is to read the platform once per combinator call (`retry`, `repeat`, `timeout` already read the
clock once per call, not per iteration) and, for `Flow.sleep` loops, to capture at the loop. If that is still not within noise, `PlatformServices` goes into
the executor's per-run state instead; that is a different design and would come back for review.

## 8. `platform-env-class.md`

With Clock, Guid, Random and Log out of the env, an application's platform boilerplate shrinks to the two or three `IHas*` lines for capabilities that
really carry authority (`IHasEnvironmentVariables`, later `IHasFileSystem`). A base class no longer earns its place. Recommendation: mark the sketch
withdrawn and point it at this document. Revisit only if T25b leaves envs with many capability lines.

## 9. Alternatives considered

- **One `Platform` field on the env plus `IHasPlatform`.** Keeps all reads explicit and zero runtime-context cost. Rejected: tests would still build a
  platform record, every env carries one more field, and libraries that sleep or log still force it on their callers.
- **Keep `IHasClock` but make it optional.** F# cannot express an optional constraint.
- **A public service registry in the runtime context.** Rejected by the owner: it is the hidden environment of ZIO.
- **Static-abstract capability lookup.** A separate topic (`richer-effects.md`); it does not change Platform.
- **Auto-advancing test clock.** Rejected: quiescence counting guesses, and fails under load.

## 10. Migration

Per environment: delete the `Clock`, `Log`, `Random`, `Guid` and `SecureRandom` fields and their `IHas*` implementations. Per call site: delete `IHasClock`
etc. from constraints; `Log.service` becomes `Log.info`; scripted randoms and guids move from env fields to `Flow.withRandom`/`withGuid`. Tests that used
`runOnManualTime` fork, `TestClock.advance`, and join. Applications that supplied a custom clock (for example from `IServiceProvider`) install it once at the
edge with `Flow.withClock`; `App.start` gains matching options. 0.12 release notes and the docs pages for clock, random-and-guid, log and test runtime are rewritten.

## 11. Order of work after approval

1. Invariant tests, failing (section 6), plus the benchmark harness for section 7 (baseline numbers recorded first).
2. Remove `ISecureRandom` (independent, unreleased; see Q1).
3. Move `IRandom`/`IGuid` into `Axial`; add `PlatformServices` and `LogSink` to the runtime context; `with*`; `Flow.sleep` and the combinators lose `IHasClock`.
4. `ILogSink`, console default, `Log.*`; `ITelemetry` over existing slots; `Axial.Telemetry` bridge.
5. `TestPlatform`, `Flow.runTestWith`, `TestClock`; delete `runOnManualTime` and port its callers.
6. Hosting, Process, Elmish clock plumbing; Fable suite.
7. Docs, release notes, migration guide, benchmarks recorded in `dev-docs/benchmarks/`.

## 12. Open questions for the owner

- **Q1. Does 0.12 wait for T25a?** The change is breaking (0.12 is the next minor; a later cut is 0.13). `ISecureRandom` is unreleased and the 0.12 notes
  already advertise it, so removing it before the release costs nothing and avoids a breaking removal later. Recommendation: remove `ISecureRandom` into 0.12 now
  (step 2 stands alone); cut T25a's Platform move in the same minor only if it finishes with benchmarks; otherwise it is 0.13 and 0.12 ships without it.
- **Q2. Where do `ILogSink` and the Telemetry bridge live?** Recommendation as above: `ILogSink`/`ITelemetry` in `Axial`, `Activity` bridge in `Axial.Telemetry`, MEL bridge in `Axial.Hosting`.
- **Q3. `Log` level API shape.** Options: (a) one function per level as today; (b) plus a minimum-level gate on the sink (`ILogSink.IsEnabled: LogLevel -> bool`) so a disabled call allocates nothing. Recommendation: (a) with the gate; structured properties deferred.
- **Q4. `Clock.fixed` vs `ManualClock`.** Recommendation: they are the same type; `runTest` installs a `ManualClock` at the epoch and `Clock.fixed` is a name for "never advanced". Confirm.
- **Q5. `BaseRuntime` and `IEnvironmentVariables`.** After this, `BaseRuntime` has one field. Keep it as the standard live env or delete it and keep `IHasEnvironmentVariables` alone? Is environment-variable access a capability (T25b) rather than a Platform service? Recommendation: treat it as a capability and drop `BaseRuntime`.
- **Q6. Elmish and Process outside a flow.** Recommendation in section 4 (explicit `Clock` option, default live). Alternative: capture the runtime context at `start`, which requires starting inside a flow.
- **Q7. Optional trace mode** (section 5.3): in 0.12/T25a, or deferred to T25b with `axial capabilities`? Recommendation: ship the recording hook in T25a (small), document it in T25b.
