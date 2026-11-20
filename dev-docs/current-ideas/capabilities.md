# Capabilities: platform in the runtime, and declared authority effects

Status: **Part 1 (Platform in the runtime) is shipped.** Parts 2 and 3 (capability declarations, and the Guardrails
rules rendered from them) are specified here, not yet built. No code, TOML or lock changes for Parts 2/3 until approved.

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

## 3. Part 2 — Capability declarations (spec, not built)

### 3.1 Goal

Make every authority effect a declared capability, and make the declaration the single source for both the advice a
Guardrails finding renders and the list `axial capabilities` prints.

### 3.2 One record, two origins

A capability is data, not an interface. There is no `ICapability` type and no `System.Type` field: nothing is
discovered by reflection, and nothing needs to be.

```fsharp
// src/Axial.Guardrails/EffectCatalog.fs
type Capability =
    { Name: string            // "filesystem"
      Guardrail: string       // the package that provides it, "Axial.FileSystem"; turns its rules on
      Service: string         // "IFileSystem"
      Env: string             // "IHasFileSystem"
      Live: string            // "FileSystem.live"
      Fake: string            // "TestFileSystem"
      Rules: EffectRule list } // the BCL surface it replaces; MemberPattern data, as today
```

The record has two origins and no third:

- **Built in.** Axial's own capabilities (filesystem, console, process, configuration, http, websocket) are values in
  `EffectCatalog.fs`, compiled into the analyzer. Today's `builtInRules` regroup under them; each rule's `Guardrail`
  becomes its capability's. The existing per-package `buildTransitive` props keep switching each guardrail on.
- **Declared by the consumer.** A repository adds its own in `Axial.Guardrails.toml`:

```toml
[[capabilities]]
name = "payments"
service = "IPayments"
env = "IHasPayments"
live = "Payments.live"
fake = "TestPayments"
replaces = [
  { type = "Stripe.ChargeService", members = "*" },
  { type = "Stripe.RefundService", constructor = true },
]
```

There is no separate `Axial.Capabilities.toml` and no generated JSON. Putting consumer declarations in the policy file
means they are already protected, hashed into the policy lock, and listed by `axial policy review`: a declaration
derives rules, so it is policy, and an agent removing a `replaces` entry is a loosening a human must approve. The
policy parser's key allowlist gains `capabilities`.

### 3.3 Validation

- `name`, `service`, `env`, `live`, `fake` and a non-empty `replaces` are required. An entry with no `replaces` is
  rejected because no rule can be derived from it.
- A consumer `name` or `replaces` type that duplicates a built-in capability is rejected.
- `live` and `fake` are names shown in advice and in `axial capabilities`; the analyzer does not resolve them. A
  stale name is a documentation bug, not a build failure.

### 3.4 Commands

```text
axial capabilities             # built-in and declared: name, service, env, live, fake, replaced members
axial add service <Name>       # scaffold the service interface, IHasX, Live and Fake stubs, and the [[capabilities]] entry
```

`axial add service` writes the capability code into a project or file with the `capabilities` role and appends the
entry to `Axial.Guardrails.toml`. That edit is a policy change: it appears in `axial policy review` and a human
approves it, like any other.

### 3.5 Where the raw surface is allowed

Unchanged from today: the capability's own code sits in a project or file the policy assigns the `capabilities` role
(in this repository, `[projects].capabilities`). There is no per-capability allowlist.

---

## 4. Part 3 — Guardrails renders from the declaration (spec, not built)

### 4.1 Rule derivation

Each `replaces` entry becomes an `EffectRule`, reported as `AXG001` like every effect rule today. The advice is
rendered from the capability, not from a hand-written string:

```text
src/Foo.fs:12 — touches System.IO.File.ReadAllText directly.
Use IFileSystem (capability "filesystem"; add IHasFileSystem to env; fake: TestFileSystem).
```

Three fixed shapes:

| Situation | Advice |
|---|---|
| Platform member (`Clock`/`Guid`/`Random`/`Culture`/`TimeZone`) | `use Clock.now`; the runtime provides it (unchanged, from 2.7) |
| Member a capability replaces | `use IFileSystem; add IHasFileSystem to env` |
| No capability replaces it | `no capability replaces this; run axial add service <Name>` |

### 4.2 Suppressions are unchanged

No new silencer syntax. An exceptional call site keeps today's form, `// axial-allow-AXG001: <reason>`, effective only
in a file listed under `[boundaries]`; `AXG002` still rejects a suppression that suppresses nothing. Derived rules are
ordinary `AXG001` rules, so nothing in the suppression machinery needs to know about capabilities.

### 4.3 Close the console hole

`console.any` matches only `System.Console`. Add to the built-in console capability's rules:

- `Microsoft.FSharp.Core.ExtraTopLevelOperators.printf`, `printfn`, `eprintf`, `eprintfn`, and the `stdout` / `stderr`
  values. Advice: `IConsole.Write` / `WriteLine` / `WriteError` / `WriteErrorLine`.
- `sprintf`, `failwithf`, `kprintf`, `fprintf` (to a writer the code was given) and `String.Format` stay allowed:
  they format, they do not write to the process.

Exit is not a console member, and `IConsole` gains none. `exit` (`Microsoft.FSharp.Core.Operators.exit`) and
`System.Environment.Exit` get a rule in the core guardrail whose advice is the host: return the flow's `Exit` from
`DotNetApp.run` (or the Node host), which maps it to the process exit code with `exitCode`. Terminating from inside a
flow skips finalizers, so it has no capability to replace it.

Measure the printf rule on the examples and tests before turning it on; it is the one addition likely to be noisy.

---

## 5. Order of work

1. Approve Part 2/3 shape.
2. Add the `Capability` record and regroup `builtInRules` under the six built-in capabilities, with no change to any
   finding's text. `axial capabilities` lists them.
3. Render advice from the capability (4.1); update the advice snapshot tests.
4. Parse `[[capabilities]]` in `Axial.Guardrails.toml` (validation from 3.3, review and lock coverage, key allowlist).
5. `axial add service`.
6. Console printf/stdout rules and the core exit rule (4.3), after measuring them on this repository.
7. Docs + release notes + migration.

---

## 6. Non-goals (unchanged)

- No general "register any service in the runtime context" API. Platform and observation are ambient; authority stays
  in `'env`.
- No auto-advancing test clock, no quiescence detection.
- No reflection-based discovery: built-in capabilities are compiled values, consumer ones are TOML data.
- `Schedule<'env,'input,'output>` keeps `'env`; only the clock requirement left it.
