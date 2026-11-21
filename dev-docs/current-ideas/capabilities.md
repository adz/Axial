# Capabilities: platform in the runtime, and declared authority effects

Status: **Part 1 (Platform in the runtime) is shipped.** Part 2 (capabilities as guidance) is built on
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

## 3. Part 2 — Capabilities as guidance (built on `feature/capabilities`)

### 3.1 Principle

A capability exists to tell the person or agent at the call site what to use instead, not to add a check someone must
approve. So declaring one needs no policy change, and a declared capability's findings are information unless its
owner opts into enforcement. Nothing here adds a protected file, a lock entry or an approval step.

### 3.2 The record

`EffectCatalog.Capability` holds what a finding needs to be useful: `Service`, `Env`, `Field`, `Example`, `Live`,
`Fake`, `Level` (`Guide` | `Enforce`) and the `Guardrail` its rules belong to. Each `EffectRule` names its capability.
There is no interface, no `System.Type` and no reflection.

Two origins:

- **Axial's own**, compiled into the analyzer: platform `clock`, `random`, `guid`, `timezone` (no `Env`; the runtime
  provides them) and `console`, `filesystem`, `process`, `http`, `websocket`. They are `Enforce`, so the built-in
  rules keep their policy behaviour; only their advice changed.
- **Declared** with an `<AxialCapability>` item in a project file or `Directory.Build.props`, written into the
  rules file next to `AxialGuardrailRule` lines. Defaults fill `Env` (`IHas<Name>`), `Field` (`<Name>: I<Name>`),
  `Level` (`guide`) and `Guardrail` (the capability's name, enabled by the declaration). `Replaces` uses the existing
  match grammar, separated by `|`. A malformed or duplicate declaration fails the build with its reason.

### 3.3 Findings

Every effect finding renders its advice from the capability:

| Capability | Advice |
|---|---|
| Platform | `Use Clock.now. The runtime provides Clock, so the environment does not change; tests control it with ...` |
| In `'env` | `Use IFileSystem: give the environment record a FileSystem: IFileSystem field and implement IHasFileSystem, then call it, for example ... Production: ... Tests: ...` |
| None | `Use <replacement>. If no service covers this effect yet, axial add service <Name> scaffolds one ...` |

A `Guide` capability's finding has type `Axial Capability Guide` and `Info` severity in Fast and Full. It is never
baselined, never escalated at a checkpoint and needs no `axial-note`. A file that declares or implements the service
(`type I<Name>`, `interface I<Name> with`, `new I<Name> with`) gets no guide finding. A definition of a wrapped type
is not a use.

### 3.4 Commands

- `axial capabilities [--json]` lists Axial's capabilities and every declaration found in built projects' rules
  files, with the project that declared it.
- `axial add service <Name> [--dir <path>] [--replaces <match>]` writes `<Name>.fs` (error union, service interface,
  environment interface, accessor, live stub, fake) and prints the `<AxialCapability>` item. It does not edit the
  project file.

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
