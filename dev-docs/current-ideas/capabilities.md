# Capabilities: platform in the runtime, and declared authority effects

Status: **Part 1 (Platform in the runtime) is shipped.** Parts 2 and 3 (capability declarations, and the Guardrails
rules that consume them) are specified here, not yet built. No code, TOML or lock changes for Parts 2/3 until approved.

Inputs: this file supersedes `platform-env-class.md` (withdrawn); `richer-effects.md` and `flow-as-data.md` remain the
future-compatible neighbours.

---

## 1. Terminology

- **Platform** — values a flow *reads* and every correct fake is deterministic: clock, random, guid, machine culture,
  machine time zone. They live in the runtime context, not `'env`.
- **Observation** — write-only sinks that a flow *emits into*: log, telemetry. They live in the runtime context.
- **Authority** — effects that can change the world or read external state the app does not own: files, directories,
  environment variables, console I/O, processes, HTTP, secrets. They stay in `'env` as explicit capabilities.
- **Capability** — a declared authority service with a `Live` implementation, a `Fake`, and the set of BCL members it
  `Replaces`.

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

Make every authority effect a declared capability, and make the declaration the single source for both the service and
the Guardrails rule that bans its raw BCL surface.

### 3.2 The declaration

```fsharp
/// A declared authority capability. Not a runtime registry entry: it still flows through 'env as IHasX.
type ICapability =
    abstract Name: string
    /// The BCL members this capability replaces; Guardrails derives its rule from these.
    abstract Replaces: MemberPattern list
    /// The service interface and its env accessor.
    abstract Service: Type
    /// The live implementation and its test fake.
    abstract Live: string
    abstract Fake: string
```

`MemberPattern` is the existing catalog vocabulary: `MemberOf(typeName, members)`, `AnyMemberOf typeName`,
`ConstructorOf typeName`. It is data, not a prose string.

### 3.3 The manifest

`Axial.Capabilities.toml` (or a generated `capabilities.json`) lists every capability in the repository:

```toml
[[capabilities]]
name = "filesystem"
package = "Axial.FileSystem"
service = "IFileSystem"
env = "IHasFileSystem"
live = "FileSystem.live"
fake = "FileSystem.inMemory"          # or TestFileSystem
replaces = [
  { type = "System.IO.File", members = "*" },
  { type = "System.IO.Directory", members = "*" },
  { type = "System.IO.FileSystemWatcher", constructor = true },
]
```

### 3.4 Commands

```text
axial capabilities             # list declared capabilities: name, service, live, fake, replaces
axial add service <Name>       # scaffold a capability module + service interface + IHasX + manifest entry
```

`axial add service` writes the capability-role module, not domain code; the human fills in `Live` and `Fake`.

### 3.5 Rules

- A capability module is the only place its raw BCL surface is allowed, and it carries the file-level
  `capabilities` role in `Axial.Guardrails.toml` — not a per-call suppression.
- `Live` + `Fake` + `Replaces` must all be present for a capability to be valid. A manifest entry with no `Replaces`
  is rejected, because there is then no rule to derive.

---

## 4. Part 3 — Guardrails consumes the manifest (spec, not built)

### 4.1 Rule derivation

Every `replaces` entry becomes one rule. The diagnostic is rendered from the declaration, not from a hand-written
string:

```text
src/Foo.fs:12 — touches System.IO.File.ReadAllText directly.
Use IFileSystem.ReadAllText (capability "filesystem"; add IHasFileSystem to env).
```

Three fixed shapes:

| Situation | Advice |
|---|---|
| Platform member (`Clock`/`Guid`/`Random`/`Culture`/`TimeZone`) | `use Clock.now` — runtime provides it |
| Authority member with a matching capability | `use IFileSystem.X — add IHasFileSystem to env` |
| No matching capability | `no capability replaces this; run axial add service <Name>` |

### 4.2 The reason silencer

The per-line silencer stays a comment above the offending line and must name the capability and the reason:

```fsharp
// axial-note AXG001: use IFileSystem.ReadAllText because this is a settings boundary, not a new capability.
```

`AXG002` continues to reject a suppression that suppresses nothing, and a suppression naming a capability that is not
in the manifest is invalid.

### 4.3 Close the console hole

`console.any` currently matches only `System.Console`. Add the F# top-level output and exit members to the console
capability's `Replaces` so they cannot bypass the `IConsole` rule:

- `Microsoft.FSharp.Core.ExtraTopLevelOperators.stdout` / `stderr`
- `Microsoft.FSharp.Core.ExtraTopLevelOperators.exit`
- `Microsoft.FSharp.Core.PrintfModule` members that write to `stdout`/`stderr`

They map to `Axial.Console.IConsole` (write/error/exit).

---

## 5. Order of work

1. Approve Part 2/3 shape.
2. Land the manifest schema + `axial capabilities` (no rule changes yet).
3. Land `axial add service` and migrate the existing `Axial.FileSystem`/`Axial.Console`/`Axial.Process`/`Axial.Configuration`
   services into manifest entries.
4. Switch `EffectCatalog` rule rendering to read from the manifest (keeping today's strings as the fallback during the cutover).
5. Add the console `stdout`/`stderr`/`exit` entries.
6. Docs + release notes + migration.

---

## 6. Non-goals (unchanged)

- No general "register any service in the runtime context" API. Platform and observation are ambient; authority stays
  in `'env`.
- No auto-advancing test clock, no quiescence detection.
- No reflection-based discovery: the manifest is generated/checked, not scanned at runtime.
- `Schedule<'env,'input,'output>` keeps `'env`; only the clock requirement left it.
