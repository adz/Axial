# Capabilities: platform in the runtime, and declared authority effects

Status: **Part 1 (Platform in the runtime) is shipped. Part 2 (capabilities own raw APIs) is built for 0.16.0.**
Part 3 lists what is still an idea.

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

## 3. Part 2 — Capabilities own raw APIs (0.16.0)

### 3.1 Principle

Raw effects are blocking errors the agent must fix, with advice precise enough to act on. One mechanism decides where a
raw API may be called: a capability, claimed file by file. Leniency is counted, never hidden in a file or project that
allows everything. Policy review judges changes by direction, so only loosening reaches a human.

### 3.2 Definitions and claims

- **Definition.** Axial's own capabilities are compiled into `EffectCatalog.builtInCapabilities` (`clock`, `random`,
  `guid`, `timezone`, `blocking`, `processors`, `environment`, `console`, `filesystem`, `process`, `http`,
  `websocket`). Anyone else's is a `<name>.capability.toml` file (`name`, `service`, `replaces`, and optional `env`,
  `field`, `example`, `live`, `fake`, `level`, `guardrail`), named by an `<AxialCapabilityDefinition Include="…">`
  item. A package ships both in `buildTransitive`. A definition fails the build when it is malformed, has an unknown
  key, duplicates a name, or replaces an API that another capability or rule covers.
- **Claim.** `AxialCapability="name[;name]"` metadata on a file's `Compile` item. Only claimed files may call the API,
  and a claimed file may call only the APIs it claims. A claim of an undefined capability fails the build. Claims are
  per project: a file compiled into two projects is claimed in each.
- **Gap allowance.** `// axial-allow-AXG001: reason (gap: name)` allows one call in any file, never a whole file. It
  marks a missing feature in Axial or a capability; `axial check` lists them by gap.
- `[boundaries]` is gone: `axial check` warns until the toml drops it. `host` stays the one role that allows raw calls,
  because a composition root assembles live services.

### 3.3 Verdicts

`EffectUse.judge` decides every raw effect call the same way in Fast and Full:

| Verdict | When | Counted |
|---|---|---|
| Owned | The file claims the capability that owns the API | yes, per capability |
| Allowance | An `axial-allow-AXG001` comment that names its gap | yes, per gap |
| Host | A `host`-role file, or `effects.ambient = allow` | yes |
| Reported | Anything else, at the role's level (`forbid` always reports) | finding |

### 3.4 Review by direction

The lock records definitions (`DEFINITION name level replaces`) and claims (`CLAIM project capability files`).
`PolicyLock.capabilityChanges` compares two snapshots; the build's drift warning and `axial policy review` (the release
gate) both use it.

| Loosens (review) | Tightens or neutral (free) |
|---|---|
| A definition removed | A definition added |
| A `replaces` entry removed | A `replaces` entry added |
| `enforce` → `guide` | `guide` → `enforce` |
| A project claims a capability for more files | A claim removed, or a claimed file renamed or moved (same count) |
| An allowance added (as every suppression) | Text fields (`example`, `fake`, …) |

### 3.5 Advice and visibility

Findings are instructions: "Replace it with IPayments: add a `Payments: IPayments` field to the environment record,
implement IHasPayments, and call the service (for example ...). Tests supply ...". An effect no capability covers
offers a definition to write, with the match filled in; structural rules (blocking waits, raw concurrency) give only
their rewrite. In a claimed file, a foreign effect adds which capability the file claims.

Each build result records `effectUses` per file (owned per capability, allowances per gap, host, reported).
`axial check` prints the allowed counts per project (`--json`: `allowedEffects`); `axial capabilities` lists every
capability with the files that claim it.

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

1. ~~Part 2: definitions, claims, gap allowances, review by direction.~~ Built for 0.16.0 (release notes in
   `dev-docs/releases/0.16.0.md`).
2. Convert the repository's tests: rewrite blocking waits and delays as flows on `TestPlatform`, and claim the
   capability on test files that check the real thing.
3. Measure and add the printf/stdout rules; add the exit rule.

---

## 6. Non-goals (unchanged)

- No general "register any service in the runtime context" API. Platform and observation are ambient; authority stays
  in `'env`.
- No auto-advancing test clock, no quiescence detection.
- No reflection-based discovery: built-in capabilities are compiled values, declared ones are MSBuild items.
- `Schedule<'env,'input,'output>` keeps `'env`; only the clock requirement left it.
