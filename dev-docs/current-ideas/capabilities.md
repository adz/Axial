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

/// One log record. Annotations are the fiber's current annotations (Flow.annotate) and Locals its named FiberLocals, so a sink sees
/// trace_id and scoped values without the caller threading them. Both are snapshots taken at the call.
type LogEntry =
    { Level: LogLevel
      Message: string
      Error: exn option
      Annotations: Map<string, string>
      Locals: (string * obj) list }

/// Write-only. Anyone may implement one. Must not throw; a throwing sink is guarded, like fiber observers.
type ILogSink =
    /// Asked before an entry is built, so a disabled call allocates nothing.
    abstract IsEnabled: LogLevel -> bool
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

### 3.3a Why the runtime context, and why it is an `AsyncLocal`

Platform has to be readable by `Flow.sleep` and friends without appearing in `'env`, and inherited by every forked fiber. A `Flow` is
`'env -> CancellationToken -> Execution`: it has no third parameter to carry per-run state, so the only existing carrier is `RuntimeState`
(an `AsyncLocal` on .NET, a plain cell plus `captureAmbient`/`restoreAmbient` on Fable). Scope, observer, annotations and tracer already travel
that way, which is why forks inherit them for free.

This is a property of the current executor, not a requirement of the design. The alternatives are a runtime parameter on every `Flow`
function (a rewrite of the hot path, and a visible change to every combinator) or per-run state owned by an interpreter, which is what
Flow-as-data (T26) provides. To keep that door open, **all reads go through one internal accessor** (`Platform.current ()`), so moving the state out of
`AsyncLocal` later changes one function. The benchmark in section 7 is what decides whether the `AsyncLocal` read is acceptable now.

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

`Clock.fixed t` is a `ManualClock` starting at `t` that nothing advances automatically; it is not a different type. Tests still move it
explicitly (`TestClock.advance`, or `ManualClock.AdvanceBy` on `TestPlatform.Clock`). A flow that sleeps under it and is never advanced hangs, deliberately (see 3.6).

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
sink's level gate passes (decision D3).

Telemetry stays an observation slot. Today the slots are internal (`Telemetry.withSink` is hidden); `ITelemetry` makes them public and
`Flow.withTelemetry` composes them. The `Activity`/`ActivitySource` bridge lives in **`Axial.Telemetry`** (the only package that already boxes the
tracer), not in `Axial`, which keeps no dependency on `System.Diagnostics.DiagnosticSource`. The bridge is `Telemetry.withActivitySource source`. A
bridge from `ILogSink` to `Microsoft.Extensions.Logging` goes in `Axial.Hosting`, next to the existing logger-backed fiber observer.

### 3.8 Installing a log sink: manual, composed, and by the hosts

Precedence, innermost wins: the runtime default (console, Info and above, stderr), then whatever the host installs at the root, then any
`Flow.withLogSink` inside the application.

**Manual.**

```fsharp
Flow.withLogSink   : ILogSink -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>               // replace
Flow.mapLogSink    : (ILogSink -> ILogSink) -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>  // decorate the sink already in force

module LogSink =                                        // combinators, all pure
    val tee      : ILogSink -> ILogSink -> ILogSink     // both receive each entry; each guarded
    val minLevel : LogLevel -> ILogSink -> ILogSink
    val filter   : (LogEntry -> bool) -> ILogSink -> ILogSink
    val map      : (LogEntry -> LogEntry) -> ILogSink -> ILogSink   // for example, prefix a category
    val console  : LogLevel -> ILogSink                 // the default, at a chosen level
    val none     : ILogSink
    val fromFunction : (LogEntry -> unit) -> ILogSink   // IsEnabled is always true
```

`mapLogSink` is what makes "add a second destination for this subtree" or "silence Debug inside this subtree" a one-liner without reading the current sink out
of the context. `App.start` and `App.startWith` take the sink as an option (`AppOptions.LogSink`), which is just `withLogSink` at the root.

**Generic Host (`Axial.Hosting`).** Today `MicrosoftLogging.create : ILogger -> ILog` and `layer : Layer<ILoggerFactory, Never, ILog>` produce an env service, and
`FiberLogging.observe` installs a fiber observer. After:

- `MicrosoftLogging.sink : ILogger -> ILogSink` and `MicrosoftLogging.fromFactory : category -> ILoggerFactory -> ILogSink` replace `create`/`fromFactory`.
  The layer is deleted (nothing to put in an env). `IsEnabled` is `logger.IsEnabled`, so the host's own filtering applies with no allocation. `Write` maps `Locals` to
  `logger.BeginScope` exactly as `ScopedLogging.withScope` does now, and adds `Annotations` to the same scope.
- `FlowHostedService` installs `MicrosoftLogging.sink` over the host's `ILoggerFactory` around the root application, category `Axial`, unless
  `HostedAppOptions.LogSink` supplies another (`ILoggerFactory -> ILogSink`, or `None` to leave the runtime default). Applications that registered
  `MicrosoftLogging.layer` today delete that line and get the same output.
- The fiber observer stays opt-in: `FiberLogging.observe` is a separate concern (defects), it is not a log sink, and installing it silently would change
  behaviour. A later change may fold it into the hosted-service default.
- The watchdogs (`watchdog`, `watchdogWith`) drop `IHasClock` and `IHasLog` and call `Log.*`.

**ASP.NET Core (`Axial.Hosting.AspNetCore`).** `flowEndpoint` starts each request as its own root run (`workflow.StartAsTask`), so a host-level install does not reach it.
`flowEndpoint` installs `MicrosoftLogging.sink` over `context.RequestServices`' `ILoggerFactory`, category `Axial.Endpoint`, around the request flow. MEL
scopes opened by ASP.NET stay in effect because the sink calls the same logger on the same async flow. An `EndpointOptions.LogSink` overrides it.

**GenHttp.** GenHttp has no logging abstraction. `flowEndpoint` leaves the runtime default in force unless `EndpointOptions.LogSink` is set; the application
composes whichever sink it wants with `LogSink.fromFunction`.

**Node and Browser (Fable).** The runtime default on Fable cannot assume `System.Console.Error`. `Axial.Hosting.Node` provides `NodeLogging.sink` (`console.error`
with a level prefix, honouring a minimum level) and `NodeApp.start` installs it. `Axial.Hosting.Browser` provides `BrowserLogging.sink` mapping levels to
`console.debug/info/warn/error`, installed by its entry point. Whether Axial's own default sink can use Fable's `Console` mapping directly is checked by invariant test 9; if it can, these two
install the same sink with Node/browser-specific formatting only.

**Telemetry packages.** `Axial.Telemetry` and `Axial.Telemetry.JavaScript` do not install a log sink. A `Telemetry.logSink` that records entries as span events on the
current `Activity` is possible and is deferred: it composes with `LogSink.tee` and needs no runtime change.

**Why the sink is installed per run and not once per process.** A flow started by anything other than the host (a background `StartAsTask`, a test, a
tool) gets the console default, never silence and never a hidden global. The only global is the default itself.

## 4. What gets removed (the breaking change)

| Removed | Replacement |
|---|---|
| `IHasClock`, `IHasLog`, `IHasRandom`, `IHasGuid` | Runtime context; delete the field and the interface line from your env |
| `ClockEnvironment` | Not needed: `Flow.runTest ()` or any env |
| `ISecureRandom`, `IHasSecureRandom`, `SecureRandom` module | Nothing. Use `System.Security.Cryptography` inside a capability |
| `ILog`, `IHasLog`, `Log.service`, `MicrosoftLogging.create`/`layer` | `ILogSink`, `Flow.withLogSink`/`mapLogSink`, `Log.info`, `MicrosoftLogging.sink` (host installs it) |
| `BaseRuntime` and `servicesFromServiceProvider` | `IEnvironmentVariables` is a capability, not Platform (it reads external state): `IHasEnvironmentVariables` stays on the env; T25b declares it |
| `TestRuntime` env members | `TestPlatform` |
| `runOnManualTime` | Explicit stepping |
| `platform-env-class.md` sketch | Withdrawn (section 8) |

`ISecureRandom` removal is unreleased, so it costs no user. Its Guardrails rule (`RandomNumberGenerator`) keeps firing but now says: use
`System.Security.Cryptography` inside capability code. Files touched: `Services.fs`, `Operations.fs`, `Platform.fs`, `EffectCatalog.fs`,
`SecureRandomTests` in PlatformService and Fable tests, `RuntimeFoundationTests`, `GuardrailConfigTests`, docs `03-random-and-guid.md`, 0.12 notes.

Callers outside a Flow's dynamic extent (decision D6) need an explicit clock: `Axial.ElmishFlows` (`ElmishRuntime<'env when 'env :> IHasClock>`), `Axial.Process`
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
2. Remove `ISecureRandom` (independent, unreleased; decision D1).
3. Move `IRandom`/`IGuid` into `Axial`; add `PlatformServices` and `LogSink` to the runtime context; `with*`; `Flow.sleep` and the combinators lose `IHasClock`.
4. `ILogSink`, console default, `LogSink` combinators, `Log.*`; `ITelemetry` over existing slots; `Axial.Telemetry` bridge.
5. `TestPlatform`, `Flow.runTestWith`, `TestClock`; delete `runOnManualTime` and port its callers.
6. Hosting (Generic Host, ASP.NET Core, GenHttp, Node, Browser) log-sink installation; Process and Elmish clock plumbing; Fable suite.
7. Docs, release notes, migration guide, benchmarks recorded in `dev-docs/benchmarks/`.

## 12. Decisions that closed the open questions (owner, 2026-10-30)

- **D1. `ISecureRandom` is removed now**, ahead of the rest, and 0.12 ships without it (step 2 of section 11 stands alone). The rest of T25a is breaking and lands
  in whichever minor it finishes in with benchmarks (0.12 if ready, else 0.13).
- **D2. Where things live.** `ILogSink`, `ITelemetry`, `IRandom`, `IGuid` in `Axial`; the `Activity`/`ActivitySource` bridge in `Axial.Telemetry`;
  the `Microsoft.Extensions.Logging` bridge in `Axial.Hosting`.
- **D3. Log API.** One function per level, as today, plus a level gate on the sink (`ILogSink.IsEnabled: LogLevel -> bool`) so a disabled call allocates
  nothing. Structured properties are deferred.
- **D4. `Clock.fixed` and `ManualClock` are one type**, and time can always be advanced explicitly (`TestClock.advance`).
- **D5. `BaseRuntime` is dropped.** Environment variables are a capability (T25b), not Platform. `servicesFromServiceProvider` goes with it.
- **D6. Elmish and Process outside a flow** take an explicit `Clock` (default live; `Elmish.test` takes a `ManualClock`); Hosting and Cache read the runtime
  context from inside their flows.
- **D7. Trace mode.** The recording hook for Clock/Random/Guid reads ships in T25a (`TestPlatform.trace`); T25b documents it and surfaces it in `axial capabilities`.
- **D8. `platform-env-class.md` is withdrawn** (section 8).
- **D10. Log adapters.** `Flow.withLogSink` replaces, `Flow.mapLogSink` decorates, and `LogSink` has `tee`, `minLevel`, `filter`, `map`, `console`, `none`, `fromFunction`.
  `ILogSink` has `IsEnabled` and `Write`; `LogEntry` carries Annotations and named FiberLocals (the latter feed MEL scopes).
- **D11. Hosts install the sink.** Generic Host installs the MEL sink around the root app (category `Axial`); ASP.NET Core `flowEndpoint` installs it per request
  (category `Axial.Endpoint`); GenHttp leaves the default unless `EndpointOptions.LogSink` is set; Node and Browser ship their own console sinks. Each can be overridden or turned off by an option.
- **D12. The fiber observer is not a sink.** `FiberLogging.observe` stays opt-in and unchanged.
- **D9. Carrier.** The runtime context stays in `RuntimeState` (`AsyncLocal` on .NET) behind one accessor; section 3.3a and the section 7 benchmark decide whether that is acceptable.

Smaller points settled here so they do not stay open:

- `ITelemetry` is exactly the two members in 3.1; the ambient tracer stays an opaque slot only `Axial.Telemetry` touches.
- `LogEntry.Annotations` is a snapshot of the fiber's annotations at the call, so a sink sees `trace_id` without the caller passing it.
- A throwing sink is guarded like a fiber observer: it cannot alter a flow's outcome (invariant test 8).
- `Schedule<'env,'input,'output>` keeps its `'env`; only the clock requirement leaves it.
- `App.start` gets `Clock`, `LogSink` and `Telemetry` options that install at the root; nothing else in `App` changes.
- Fable: the override and `captureAmbient` behaviour is proven by invariant test 9 before any Fable-facing change merges.

Nothing remains open for T25a. What follows from it is the generation of envs from specs, which is its own design (T25b territory).
