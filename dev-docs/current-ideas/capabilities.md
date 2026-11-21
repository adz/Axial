# Capabilities: platform in the runtime, and declared authority effects

Status: **Part 1 (Platform in the runtime) is shipped.** Part 2 (capabilities own raw APIs) is built on
`feature/capabilities`, awaiting review. Part 3 lists what is still an idea.

Inputs: this file supersedes `platform-env-class.md` (withdrawn); `richer-effects.md` and `flow-as-data.md` remain the
future-compatible neighbours.

---

## 1. Terminology

- **Platform** — values a flow *reads* and every correct fake is deterministic: clock, random, guid, machine culture,
  machine time zone. They live in the runtime context, not `'env`.
- **Observation** — write-only sinks that a flow *emits into*: log, telemetry. They live in the runtime context.
- **Authority** — effects that can change the world or read external state the app does not own: files, directories,
  environment variables, console I/O, processes, HTTP, secrets. They stay in `'env` as explicit capabilities.
- **Capability** — a declared authority service with a `Live` implementation, a `Fake`, and the BCL members it
  replaces.

The split is load-bearing: Platform and observation are ambient and harmless (a bad clock read is a value, not an
action); authority must remain visible in the type.

---

## 2. Part 1 — Platform in the runtime (shipped)

### 2.1 The runtime context

`RuntimeContext` (internal) carries one `Platform` field and one `LogSink` field, inherited by forks, branches and
disconnected fibers.

```fsharp
type internal PlatformServices =
    { Clock: IClock
      Random: IRandom
      Guid: IGuid
      Culture: unit -> Culture
      TimeZone: unit -> Axial.TimeZone
      Time: Platform.ITimeSource }

type internal RuntimeContext =
    { Platform: PlatformServices
      LogSink: ILogSink
      ... }
```

`IClock`, `IRandom`, `IGuid`, `ILogSink`, `ITelemetry`, `LogLevel`, `LogEntry` live in `Axial`. `RuntimeContext.create`
installs `Platform.live` and `LogSink.console LogLevel.Information` (stderr). `Axial.Telemetry` is the only consumer of
the tracer slot; no `System.Diagnostics.DiagnosticSource` dependency exists in `Axial`.

### 2.2 Installing and overriding

```fsharp
module Flow =
    val withClock     : IClock     -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withRandom    : IRandom    -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withGuid      : IGuid      -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withLogSink   : ILogSink   -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withTelemetry : ITelemetry -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val mapLogSink    : (ILogSink -> ILogSink) -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
```

Each applies to the wrapped flow and everything it forks, and not outside it. `with*` replace; `mapLogSink` decorates
the sink already in force.

### 2.3 Platform flows

No `IHasClock`/`IHasRandom`/`IHasGuid` constraints remain in the public API.

```fsharp
Flow.sleep : TimeSpan -> Flow<'env,'error,unit>

module Clock =
    val now                  : Flow<'env,'error,DateTimeOffset>
    val elapsed              : Flow<'env,'error,TimeSpan>
    val timed                : Flow<'env,'error,'value> -> Flow<'env,'error,'value * TimeSpan>
    val utcDateTime / unixTimeSeconds / unixTimeMilliseconds : Flow<'env,'error,_>

module Random =
    val next        : Flow<'env,'error,int>
    val nextMax     : int -> Flow<'env,'error,int>
    val nextInt     : int -> int -> Flow<'env,'error,int>
    val nextDouble  : Flow<'env,'error,float>
    val nextBytes   : byte array -> Flow<'env,'error,unit>

module Guid =
    val newGuid : Flow<'env,'error, System.Guid>

module Culture =
    val system : Flow<'env,'error,Culture>

module TimeZone =
    val local : Flow<'env,'error, Axial.TimeZone>

module Log =
    val trace / debug / info / warning / error / critical : string -> Flow<'env,'error,unit>
    val errorExn / criticalExn : exn -> string -> Flow<'env,'error,unit>
    val live   : ILogSink
    val memory : unit -> TestLog
```

`Clock.live`, `Random.live` and `Guid.live` expose the live implementations for code that installs them explicitly.

`Flow.sleep`, `timeout`, `retry`, `repeat`, `supervise`, `fork`, `Cache.make`, `FlowStream.fromSchedule`,
`groupedWithin`, and `throttle` read the clock from the runtime context, not `'env`.

### 2.4 Log sinks

```fsharp
module LogSink =
    val console      : LogLevel -> ILogSink
    val none         : ILogSink
    val fromFunction : (LogEntry -> unit) -> ILogSink
    val write        : ILogSink -> LogEntry -> unit   // guarded: a throwing sink cannot fail the flow
    val tee          : ILogSink -> ILogSink -> ILogSink
    val minLevel     : LogLevel -> ILogSink -> ILogSink
    val filter       : (LogEntry -> bool) -> ILogSink -> ILogSink
    val map          : (LogEntry -> LogEntry) -> ILogSink -> ILogSink
```

`LogSink.write` is public so a call site can emit one entry without wrapping it in `Flow.run`.

### 2.5 Test entry points

```fsharp
type TestPlatform =
    member Clock : ManualClock                // epoch start, settable; createAt t sets a different start
    member Log   : TestLog                    // recording ILogSink
    member Random: IRandom with get, set      // seeded 0
    member Guid  : IGuid with get, set        // sequential
    member Culture / TimeZone                 // invariant / UTC
    member Trace : bool with get, set         // records every Platform read with its fiber id
    member Reads : IReadOnlyList<string * int64>
    static member create   : unit -> TestPlatform
    static member createAt : DateTimeOffset -> TestPlatform

module Flow =
    val runTestWith : TestPlatform -> 'env -> Flow<'env,'error,'value> -> Exit<'value,'error>
    val runTest     : 'env -> Flow<'env,'error,'value> -> Exit<'value,'error>

module TestClock =
    val advance      : TimeSpan -> Flow<'env,'error,unit>
    val advanceAll   : Flow<'env,'error,unit>
    val sleepers     : Flow<'env,'error,int>
    val awaitSleepers: int -> Flow<'env,'error,unit>
    val driveUntilDone: Fiber<'error,'value> -> Flow<'env,'error, Exit<'value,'error>>
```

Time is explicit. `advanceAll` drains only the deadline horizon registered at entry; `awaitSleepers` waits on the
clock's sleeper-registration signal (no spin-wait); `driveUntilDone` advances until a fiber settles. There is no idle
detection.

### 2.6 Removed with Part 1

`IHasClock`, `IHasLog`, `IHasRandom`, `IHasGuid`, `ISecureRandom`, `ILog`/`Log.service`, `BaseRuntime`, `runOnManualTime`,
and the `ClockEnvironment`/`TestRuntime` env members are gone. `platform-env-class.md` is withdrawn.

### 2.7 Guardrails advice for the runtime set (shipped)

`EffectCatalog.fs` already carries deterministic replacement strings for the runtime set:

| Banned | Advice |
|---|---|
| `System.DateTime.Now/UtcNow/Today`, `System.DateTimeOffset.Now/UtcNow` | `Clock.now` |
| `System.Guid.NewGuid` | `Guid.newGuid` |
| `System.Random` | `Random.next*` (secrets → `System.Security.Cryptography` in capability code) |
| `System.Security.Cryptography.RandomNumberGenerator` | capability code only |
| `System.Diagnostics.Stopwatch` | `Clock.timed` / `Clock.elapsed` |
| `System.TimeZoneInfo.Local` | `TimeZone.local` / `TimeZone.toLocal` |
| `System.Threading.Tasks.Task.Delay` | `Flow.sleep` / `Schedule` |
| `System.Threading.Thread.Sleep` | `Flow.sleep` / `Schedule` |

---

## 3. Part 2 — Capabilities own raw APIs (built on `feature/capabilities`)

### 3.1 Principle

Raw effects are blocking errors the agent must fix, with advice precise enough to act on. Leniency exists only where
the code genuinely cannot use a service, and then it is declared as a capability and counted, never hidden in a file
that allows everything.

### 3.2 Verdicts

`EffectUse.judge` decides every raw effect call the same way in Fast and Full:

| Verdict | When | Counted |
|---|---|---|
| Owned | The file implements the capability that owns the API | yes, per capability |
| Boundary | A reasoned `axial-allow-AXG001` comment in a `[boundaries]` file | yes |
| Host | A `host`-role file, or `effects.ambient = allow` | yes |
| Reported | Anything else, at the role's level (`forbid` always reports) | finding |

No file is exempt as a whole. A `capabilities`-role file owns only its own capability's API: an Axial package owns the
built-in capability whose guardrail is its project name (`Axial.FileSystem` owns `System.IO.File`); a declared
capability owns the files its `Implementation` glob names. Every other effect in those files is reported. Measured on
this repository at the time of writing: Axial's packages report nothing new (FileSystem 80 owned, Console 39, Process
2, WebSocket 1; Axial core 29 boundary comments; Hosting 3 host).

### 3.3 Declarations

`<AxialCapability>` items name `Service`, `Replaces` (match grammar, `|`-separated) and `Implementation` (a glob,
expanded by MSBuild to full paths on `capabilityFile` lines), with defaults for `Env`, `Field`, `Level` (`enforce`)
and `Guardrail`. A declaration fails the build when it has no `Replaces` or `Implementation`, duplicates a name, or
claims an API that another capability or rule already covers: declaring `any:System.IO.File` names Axial.FileSystem
instead, so a declaration is never a way around an existing service. `Level="guide"` reports outside uses as
information while callers migrate.

### 3.4 Advice

Findings are instructions: "Replace it with IPayments: add a `Payments: IPayments` field to the environment record,
implement IHasPayments, and call the service (for example ...). Tests supply ...". Platform services say the runtime
provides them. An effect no capability covers offers the `<AxialCapability>` item to declare, with the match already
filled in; structural rules (blocking waits, raw concurrency) give only their rewrite. In a capability's own files, a
foreign effect adds which capability the file implements and that it may call only its own API.

### 3.5 Visibility

Each build result records `effectUses` per file (owned per capability, boundary, host, reported). `axial check`
prints the allowed counts per project (`--json`: `allowedEffects`); `axial capabilities` lists every capability with
its implementation files. Counts that stay high, or a capability used from several projects, mark candidates for an
effect package.

---

## 4. Part 3 — Still ideas

- **Console hole.** `printf`/`printfn`/`eprintf`/`eprintfn` and the `stdout`/`stderr` values bypass `IConsole`.
  Add them to the console capability's rules after measuring the noise on examples and tests; `sprintf` and
  `failwithf` stay allowed.
- **Exit.** `exit` and `Environment.Exit` skip finalizers. A core rule whose advice is "return the `Exit` from
  `DotNetApp.run`" fits; `IConsole` gains no exit member.
- **Capability attributes.** `[<Capability>]` on the service interface would let a library ship its declaration in
  the assembly. It needs typed-tree discovery of referenced assemblies and works only in Full mode, so the MSBuild item
  stays the declaration until that cost is justified.

---

## 5. Order of work

1. Review and merge `feature/capabilities` (Part 2).
2. Measure and add the printf/stdout rules; add the exit rule.
3. Release notes for the advice change (AXG001 text) and the new item and commands.

---

## 6. Non-goals (unchanged)

- No general "register any service in the runtime context" API. Platform and observation are ambient; authority stays
  in `'env`.
- No auto-advancing test clock, no quiescence detection.
- No reflection-based discovery: built-in capabilities are compiled values, declared ones are MSBuild items.
- `Schedule<'env,'input,'output>` keeps `'env`; only the clock requirement left it.
