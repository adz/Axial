# Configuration and time zone/culture (T32)

Status: **draft for owner review**. No code until approved. Covers T32. T31 (the 0.12 package reshape) lands first and leaves only the
environment-variable code in `Axial.PlatformService`; this document is what that code becomes.

Inputs: T31/T32 in `dev-docs/TASKS.md`, `capabilities.md` (T25a, binding), `dev-docs/decisions/README.md`, and the code as of `30c508020`:
`Axial.PlatformService` (`Services.fs`, `Platform.fs`, `Operations.fs` modules `EnvironmentVariables`, `EnvironmentVariable`,
`EnvironmentVariableErrors`), `Axial.Hosting` `Configuration`, `NodeApp.arguments`, `PlatformServices` in `src/Axial/Core.fs`, and
`Flow.withClock`/`withRandom`/`withGuid` in `src/Axial/Flow.fs`. Guiding principle: closer to Elm (pure domain, effects as values, no
exceptions or null, no hidden globals, behaviour from explicit inputs, never from env-var switches).

## 1. Problem

1. **Configuration is untyped and fails fast.** `EnvironmentVariable.getInt "PORT"` returns one `EnvironmentVariableError` and stops. A
   service with ten settings reports its mistakes one restart at a time. There is no notion of a default, a section, or a secret.
2. **One source.** Only environment variables. Command-line arguments exist only on Node (`NodeApp.arguments ()`), as a raw list. JSON
   files and Generic Host `IConfiguration` are reached by pretending they are environment variables
   (`Axial.Hosting.Configuration.environmentVariables`, whose `Set` throws `NotSupportedException`).
3. **Precedence is implicit.** Whatever builds the `IEnvironmentVariables` decides, and nothing reports which source supplied a value.
4. **Parsing is culture-leaky and target-dependent.** `getDouble` uses `NumberStyles.AllowThousands`, so `"1,5"` parses as `15`. On
   Fable the parsers drop the culture argument and use JS rules, so .NET and Fable can disagree on the same text.
5. **Time zone and culture are ambient.** Nothing in Axial provides them, so user code reads `TimeZoneInfo.Local`, `DateTime.Now` and
   `CultureInfo.CurrentCulture`, and tests depend on the machine. Guardrails flags `TimeZoneInfo.Local` but points at "an explicit
   TimeZoneInfo supplied through 'env", which does not exist.

## 2. Decisions already made (owner, T31/T32, 2026-10-31)

- The environment-variable code leaves `Axial.PlatformService` and becomes **`Axial.Configuration`**; the old NuGet id is deprecated.
- Configuration has typed reads with validation errors; sources are environment variables, command-line arguments (every host),
  config files and an `IConfiguration` adapter; precedence is explicit.
- **Authority stays in `'env`** (`IHasConfiguration`-style). Configuration is not Platform.
- **Time zone and culture join Clock/Random/Guid** in the runtime's `PlatformServices`, with `Flow.withTimeZone`/`withCulture` and fixed
  values under `runTest`. Axial code never reads `TimeZoneInfo.Local` or `CultureInfo.CurrentCulture`.

## 3. `Axial.Configuration`

### 3.1 Package and what moves

`Axial.Configuration` targets `netstandard2.1;net8.0`, is AOT-compatible and Fable-compatible, and references only `Axial`.

| From `Axial.PlatformService` | To `Axial.Configuration` |
|---|---|
| `IEnvironmentVariables`, `IHasEnvironmentVariables` | same names, namespace `Axial.Configuration` |
| `EnvironmentVariables` module (`tryGet`, `getAll`, `set`, `clear`, `expand`, `live`, `fromPairs`, `layer`) | same module, unchanged |
| `Platform.environmentVariables*` | internal to `Axial.Configuration` |
| `EnvironmentVariable` (typed `get*`), `EnvironmentVariableError`, `EnvironmentVariableErrors` | **deprecated** for 0.12, removed in 0.13 (section 5) |

`IEnvironmentVariables` keeps `Set` and `Expand`: mutating the process environment is an authority of its own, and it stays its own
capability (one interface per service). Configuration reads a **snapshot** taken from it; it never writes.

### 3.2 The capability

```fsharp
namespace Axial.Configuration

/// One resolved value and where it came from. Value is never null.
type ConfigEntry = { Value: string; Source: string }

/// Read-only, immutable configuration snapshot.
type IConfig =
    abstract TryFind: path: string -> ConfigEntry option     // path is "Db:Port"; case-insensitive (ordinal-ignore-case)
    abstract Children: path: string -> string list            // immediate child segments, in first-seen order
    abstract Describe: path: string -> string list           // where the path was looked for: "env MYAPP_DB__PORT", "arg --Db:Port"

type IHasConfig =
    abstract Config: IConfig
```

The naming rule (decisions README, 2026-08-10) gives `IHasConfig` with member `Config`. `IConfiguration` is avoided because
`Microsoft.Extensions.Configuration.IConfiguration` is open in every host file (decision C1).

- **Live**: `Config.ofSources sources`, built once at the entry point (section 3.6). It is a pure value: the effects were reading the sources.
- **Fake**: `Config.ofPairs [ "Db:Port", "5432" ]` (source name `"test"`). There is no other fake; the fake is the same type.
- `Replaces` (T25b): `System.Environment.GetEnvironmentVariable`, `GetCommandLineArgs`, `Microsoft.Extensions.Configuration` reads.

There is no reload. A snapshot is taken at start; applications that need `IOptionsMonitor` reload keep it on the MEL side (decision C9).

### 3.3 Typed reads: decoders, not reflection

A settings record is decoded by a small applicative decoder, the shape of Elm's `Json.Decode` with `and!`. Reflection over record fields
is rejected: it breaks under NativeAOT trimming, does not exist on Fable, and hides the key names (decision C4).

```fsharp
/// A description of how to read 'value from an IConfig. Pure; running it never throws.
type ConfigDecoder<'value>

/// Parses one string. The string is what the user wrote; the error is the "expected" phrase ("an integer").
type Parse<'value> = string -> Result<'value, string>

module Config =
    val required    : key: string -> Parse<'v> -> ConfigDecoder<'v>
    val optional    : key: string -> Parse<'v> -> ConfigDecoder<'v option>   // absent -> None; present but invalid -> error
    val withDefault : 'v -> key: string -> Parse<'v> -> ConfigDecoder<'v>      // absent -> default; invalid -> error, never the default
    val secret      : key: string -> Parse<'v> -> ConfigDecoder<Secret<'v>>   // required; value redacted in every error
    val section     : key: string -> ConfigDecoder<'v> -> ConfigDecoder<'v>   // prefixes every key inside with "key:"
    val list        : key: string -> ConfigDecoder<'v> -> ConfigDecoder<'v list>  // children "key:0", "key:1", ... in index order
    val succeed     : 'v -> ConfigDecoder<'v>
    val map         : ('a -> 'b) -> ConfigDecoder<'a> -> ConfigDecoder<'b>
    val validate    : ('a -> Result<'b, string>) -> ConfigDecoder<'a> -> ConfigDecoder<'b>  // cross-field rules, attributed to the section
    val andThen     : ('a -> ConfigDecoder<'b>) -> ConfigDecoder<'a> -> ConfigDecoder<'b>    // dependent reads; stops accumulating here
    val decode      : ConfigDecoder<'v> -> IConfig -> Result<'v, ConfigError list>           // pure; the list is never empty on Error
    val read        : ConfigDecoder<'v> -> Flow<'env, ConfigError list, 'v> when 'env :> IHasConfig
    val ofSources   : ConfigSource list -> IConfig
    val ofPairs     : (string * string) seq -> IConfig

/// Applicative only: BindReturn + MergeSources, no Bind. Two sequential let! do not compile, so errors always accumulate.
val config : ConfigBuilder
```

Example:

```fsharp
type Mode = Development | Production
type DbSettings = { Host: string; Port: int; Password: Secret<string> }
type AppSettings = { Db: DbSettings; Timeout: TimeSpan; Proxy: Uri option; Mode: Mode }

let db =
    config {
        let! host = Config.required "Host" Parse.string
        and! port = Config.withDefault 5432 "Port" Parse.port
        and! password = Config.secret "Password" Parse.string
        return { Host = host; Port = port; Password = password }
    }

let settings =
    config {
        let! db = Config.section "Db" db
        and! timeout = Config.withDefault (TimeSpan.FromSeconds 30.) "Timeout" Parse.duration
        and! proxy = Config.optional "Proxy" Parse.absoluteUri
        and! mode = Config.required "Mode" (Parse.oneOf [ "development", Development; "production", Production ])
        return { Db = db; Timeout = timeout; Proxy = proxy; Mode = mode }
    }
```

Recommended use, Elm-flags style: decode once at the entry point and put the typed record (not `IConfig`) into `'env`. `IHasConfig` and
`Config.read` exist for libraries that own a section (`Config.read (Config.section "Http" HttpSettings.decoder)`).

### 3.4 Errors

```fsharp
type ConfigProblem =
    | Missing                                         // no source had the key
    | Invalid of expected: string * shown: string option   // shown = None for secrets
    | Rejected of message: string                     // Config.validate / Parse.custom refinement
    | Malformed of message: string                    // a source could not be read: bad JSON (line:col), bad argument

type ConfigError = { Path: string; Problem: ConfigProblem; Source: string option; LookedIn: string list }

module ConfigErrors =
    val describe   : ConfigError -> string            // invariant culture, one line
    val describeAll: ConfigError list -> string       // one line per error, sorted by Path
```

`Missing` carries `LookedIn` from `IConfig.Describe`, so the message says how to supply it:
`Db:Host is missing (looked in: arg --Db:Host, env MYAPP_DB__HOST, file appsettings.json Db.Host)`. Errors are values in the typed
channel; nothing in the package throws. Paths are case-insensitive but errors print the decoder's spelling.

### 3.5 Parsing and secrets

`Parse` gives the same answer on .NET and Fable for the same text: each parser is written against an explicit grammar, not a culture.

| Parser | Accepts | Notes |
|---|---|---|
| `string`, `nonEmpty` | any / non-blank | `""` is a present value; `nonEmpty` rejects it |
| `int`, `int64`, `port` | `[+-]?[0-9]+`, in range | no thousands separators, no hex; `port` is 1..65535 |
| `float`, `decimal` | `[+-]?digits[.digits][e[+-]digits]` | `.` only; `"1,5"` is Invalid (today it is 15) |
| `bool` | `true`/`false`, any case | `1/0/yes/no` rejected (decision C6) |
| `duration` | invariant `c` format `[d.]hh:mm:ss[.fff]` and `250ms`, `30s`, `5m`, `2h`, `1d` | suffixes are integer-only |
| `dateTimeOffset` | ISO 8601 with offset or `Z` | a date-time without offset is Invalid |
| `guid`, `absoluteUri` | `D` format / absolute URI | |
| `oneOf` | case-insensitive table of `(text, value)` | how enums and unions are read; no reflection |
| `custom`, `map`, `refine` | user functions | `refine "a port" (fun p -> p > 0)` |

**Secrets.** `Secret<'v>` is a sealed class with a private field: `ToString()` is `"<redacted>"`, it has no structural equality, and the
value is read only with `Secret.reveal`. `Config.secret` marks the path, so `Invalid` for it has `shown = None`. Snapshots never render
values: `IConfig.Describe` and diagnostics print paths and sources only. Axial does not guess secrecy from key names (no `*Password*`
heuristics); the decoder says so (decision C7).

### 3.6 Sources and precedence

```fsharp
type ConfigSource                                      // a name plus flattened (path, value) pairs; pure data

module ConfigSource =
    val ofPairs     : name: string -> (string * string) seq -> ConfigSource
    val environment : prefix: string -> Flow<'env, 'error, ConfigSource> when 'env :> IHasEnvironmentVariables
    val ofEnvironment : prefix: string -> IReadOnlyDictionary<string, string> -> ConfigSource   // pure form, for tests
    val commandLine : args: string list -> Result<ConfigSource * positional: string list, ConfigError list>
    val json        : name: string -> text: string -> Result<ConfigSource, ConfigError list>
```

- **Precedence is the list order: later sources override earlier ones** (`Config.ofSources [ defaults; file; env; args ]`), the same
  as `ConfigurationBuilder`, so ASP.NET users read it correctly (decision C3). There is no implicit source: an empty list is empty config.
- **Environment variables**: only variables starting with `prefix` (`"MYAPP_"`; `""` takes all), prefix stripped, `__` becomes `:`.
  Reads go through `IHasEnvironmentVariables`, so a test passes `EnvironmentVariables.fromPairs`.
- **Command line**: `--key=value`, `--key value`, bare `--flag` (= `"true"` when followed by another `--` option or the end), `--` ends
  options; keys may use `:` (`--Db:Port=5`). Later duplicates win. Short options and positional typing are out of scope: this is
  configuration, not a CLI parser. Positionals are returned, not dropped. `-x` is `Malformed`.
- **JSON**: objects flatten to `a:b`, arrays to `a:0`, `a:1`; numbers keep their source lexeme (`1.0` stays `"1.0"`); `true`/`false` become
  `"true"`/`"false"`; `null` means absent. Parsed by one small hand-written reader shared by .NET and Fable (no `System.Text.Json`, which
  is a package dependency on `netstandard2.1` and behaves differently from `JSON.parse`; decision C5). Comments and trailing commas: rejected.
- **Files**: `Axial.Configuration` does not read files. The entry point reads text with `Axial.FileSystem` and hands it to `json`, so a
  config file is visibly a file read (decision C8).
- **`IConfiguration` adapter** (in `Axial.Hosting`, which already references MEL): `MicrosoftConfiguration.source : name -> IConfiguration ->
  ConfigSource` snapshots the flattened tree (the existing `flatten`). Its keys already use `:`.

Key rules: segments separated by `:`; comparison ordinal-ignore-case on every target; arrays are numeric segments; keys are stored as
first written and compared lower-invariant. `Config.section` is just a prefix, so `Config.section "Db" db` and a flat `"Db:Host"` are the same read.

### 3.7 How hosts provide it

| Host | Arguments | Environment | Files / other |
|---|---|---|---|
| .NET console (`App.run`) | `main argv` passed explicitly | `EnvironmentVariables.live` | `FileSystem.readAllText` then `ConfigSource.json` |
| Generic Host / ASP.NET Core | already in `IConfiguration` (`CreateDefaultBuilder(args)`) | same | `MicrosoftConfiguration.source "host" builder.Configuration` |
| Node | `NodeApp.arguments ()` | `NodeEnvironment.live` | Node file system |
| Browser | `BrowserApp.queryParameters ()` (new: `?key=value` from `location.search`) | none | JSON text embedded by the build or fetched by the app |

No host calls `Environment.GetCommandLineArgs()`; arguments arrive as an explicit value like Elm flags. A helper composes the common case:

```fsharp
let load argv : Flow<'env, ConfigError list, IConfig> when 'env :> IHasEnvironmentVariables =
    flow {
        let! env = ConfigSource.environment "MYAPP_"
        let! args, _ = ConfigSource.commandLine argv |> Flow.fromResult
        return Config.ofSources [ env; args ]
    }
// main: load argv |> Flow.bind (Config.decode settings >> Flow.fromResult) ... then build the env with the typed record.
```

Hosts do not install configuration in the runtime context: it is a capability in `'env`, so it never appears implicitly.

### 3.8 Testing

Decoders are tested without a flow: `Config.decode settings (Config.ofPairs [ ... ])` and compare the `ConfigError list` exactly.
Flows that use `Config.read` take an env whose `Config` is `Config.ofPairs`. Sources are tested from strings and dictionaries
(`ConfigSource.json "t" text`, `ofEnvironment`). There is no live/fake divergence to test: the live config is the same type.

## 4. Time zone and culture in `PlatformServices`

### 4.1 Values, not interfaces

Elm's `Time.Zone` is an opaque value obtained with `Time.here`. Axial does the same: a zone and a culture each have one correct "fake",
which is just another value, so they are sealed types, not interfaces (decision C10).

```fsharp
namespace Axial

[<Sealed>] type TimeZone      // .NET: wraps TimeZoneInfo; Fable: an IANA id plus an Intl.DateTimeFormat for offsets
[<Sealed>] type Culture       // a BCP 47 name ("" = invariant); .NET: wraps CultureInfo; Fable: the name, used with Intl

module TimeZone =
    val utc       : TimeZone
    val fixedOffset : TimeSpan -> TimeZone                     // "UTC+10:00"
    val ofId      : string -> Result<TimeZone, string>           // IANA ids on every target; Windows ids also on .NET
    val id        : TimeZone -> string
    val offsetAt  : TimeZone -> DateTimeOffset -> TimeSpan       // pure
    val toLocal   : TimeZone -> DateTimeOffset -> DateTimeOffset // same instant, zone's offset; pure
    val current   : Flow<'env, 'error, TimeZone>                 // reads the runtime context
#if !FABLE_COMPILER
    val ofTimeZoneInfo : TimeZoneInfo -> TimeZone
    val toTimeZoneInfo : TimeZone -> TimeZoneInfo
#endif

module Culture =
    val invariant : Culture
    val ofName    : string -> Result<Culture, string>
    val name      : Culture -> string
    val current   : Flow<'env, 'error, Culture>
#if !FABLE_COMPILER
    val formatProvider : Culture -> IFormatProvider            // for String.Format / ToString(provider)
#endif

module Clock =
    val nowLocal  : Flow<'env, 'error, DateTimeOffset>           // Clock.now |> TimeZone.toLocal current zone
```

Local-to-UTC conversion (wall-clock time to instant, with skipped and repeated hours) is **not** in T32: it needs a typed
`Ambiguous | Skipped` result and its own design (decision C11).

### 4.2 Runtime fields and copy cost

```fsharp
type internal PlatformServices =
    { Clock: IClock; Random: IRandom; Guid: IGuid; Time: Platform.ITimeSource
      TimeZone: TimeZone
      Culture: Culture }
```

`RuntimeContext` is unchanged: it holds `Platform` as one pointer, so fork/branch/disconnect copies stay the same size. The record grows
from four to six fields, which costs only at `Flow.with*` install time. `withClock` today rebuilds with `PlatformServices.Create(clock,
Random, Guid)`; it changes to `{ runtime.Platform with Clock = clock; Time = Platform.timeOfClock clock }` so it keeps the zone and culture
(invariant test 12).

```fsharp
module Flow =
    val withTimeZone : TimeZone -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
    val withCulture  : Culture  -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
```

Same scope rule as `withClock`: the wrapped flow and everything it forks. `withCulture` does **not** set `CultureInfo.CurrentCulture`
or `Thread.CurrentCulture`: Axial does not mutate ambient .NET state (decision C12).

**Live defaults.** `PlatformDefaults` is the one place allowed to read the machine: `TimeZoneInfo.Local` / `CultureInfo.CurrentCulture`
on .NET, `Intl.DateTimeFormat().resolvedOptions().timeZone` and `.locale` on Fable. Both are resolved lazily on first read, so a
run that never asks pays nothing at startup (benchmark 2). They are read once per process, like `TimeZoneInfo.Local`'s own cache.

**`runTest`.** `TestPlatform` gains `TimeZone` (default `TimeZone.utc`) and `Culture` (default `Culture.invariant`), settable like `Random`;
`trace` records `TimeZone.current` and `Culture.current` reads.

**Fable.** Offsets come from `Intl.DateTimeFormat("en-US", { timeZone, hourCycle: "h23", ... }).formatToParts(instant)`, difference from
UTC. An invalid id throws `RangeError` inside `ofId`, which catches it into `Error`. `Culture.ofName` uses
`Intl.getCanonicalLocales`. Node without full ICU: `ofId` returns `Error` for non-UTC ids; documented, tested on the CI Node version.

### 4.3 Guardrails

Rules in `EffectCatalog.fs`, all symbol matches like the existing clock rules:

| Rule | Members | Suggestion |
|---|---|---|
| `clock.local-timezone` (exists; message changes) | `TimeZoneInfo.Local` | `TimeZone.current` (runtime), `Flow.withTimeZone` |
| `clock.datetime` (exists; message adds) | `DateTime.Now`, `Today` | `Clock.nowLocal` |
| `clock.to-local` (new) | `DateTime.ToLocalTime`, `DateTimeOffset.ToLocalTime`, `TimeZoneInfo.ConvertTimeFromUtc`/`ConvertTime` with `Local` | `TimeZone.toLocal` |
| `culture.current` (new) | `CultureInfo.CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`, `Thread.CurrentCulture` | `Culture.current`, `CultureInfo.InvariantCulture` for machine text |
| `environment.state` (exists; adds) | `Environment.GetCommandLineArgs`, `Environment.CommandLine` | `main argv` + `ConfigSource.commandLine` |

Implicit-culture overloads (`Double.Parse(string)`, `ToString()` on numbers, `String.Format` without a provider) are not flagged in
0.12: they need overload resolution analysis and CA1305 already covers them (decision C13). Axial's own `src` is held to the same rules
by running Guardrails on the repo at deny (exceptions only in `PlatformDefaults`, with `axial-allow` reasons).

## 5. Migration from 0.11.3

| 0.11.3 | 0.12 |
|---|---|
| `open Axial.PlatformService` for env vars | `open Axial.Configuration`; package `Axial.Configuration` |
| `IEnvironmentVariables`, `IHasEnvironmentVariables`, `EnvironmentVariables.*` | unchanged, new namespace |
| `EnvironmentVariable.get "X"` | `Config.read (Config.required "X" Parse.string)` over `ConfigSource.environment ""` |
| `EnvironmentVariable.getInt` / `getDouble` / ... | `Config.required "X" Parse.int` / `Parse.float` (stricter: no thousands separators) |
| `EnvironmentVariable.tryGet` | `Config.optional "X" Parse.string` |
| `EnvironmentVariableError` (one, fail-fast) | `ConfigError list` (all errors); `ConfigErrors.describeAll` |
| `Axial.Hosting.Configuration.environmentVariables cfg` / `.layer` | `MicrosoftConfiguration.source "host" cfg` into `Config.ofSources` |
| `NodeApp.arguments ()` | unchanged; feed it to `ConfigSource.commandLine` |
| `TimeZoneInfo.Local`, `DateTime.Now`, `CultureInfo.CurrentCulture` in user code | `TimeZone.current`, `Clock.nowLocal`, `Culture.current` |

`EnvironmentVariable`, `EnvironmentVariableError` and `EnvironmentVariableErrors` stay in 0.12 under `[<Obsolete>]` pointing at the row
above, then go in 0.13; this is breaking (namespace move), so 0.12.0 per the minor-bump rule. Rows go in `dev-docs/releases/0.12.0.md`.

## 6. Invariant tests (written first; they must fail before implementation)

1. A decoder with three bad keys returns exactly three errors, sorted by path, from one `decode`.
2. `withDefault` returns the default when absent and an error (not the default) when present but invalid.
3. Precedence: the same key in env and args yields the args value with `Source = "args"`; reversing the list reverses the winner.
4. `Missing` lists every source's spelling of the path (`--Db:Host`, `MYAPP_DB__HOST`, `Db.Host`).
5. A secret's invalid value never appears in `describe`, `describeAll`, `ToString`, `sprintf "%O"`, or a log entry built from the error.
6. Parser table: every row of 3.5 (and the rejected inputs: `"1,5"`, `"0x10"`, `"yes"`, offsetless dates) gives the same result on .NET and Fable.
7. Parsers are culture-independent: the .NET suite runs the table under `de-DE` and `tr-TR` thread cultures with identical results.
8. JSON flattening: nested objects, arrays, `null`, number lexemes, unicode escapes; malformed text gives `Malformed` with line:col; same on Fable.
9. Command line: `--a=1 --b 2 --flag -- --c` gives `a`, `b`, `flag=true` and positional `--c`; `-x` gives `Malformed`.
10. Keys are case-insensitive on Fable and .NET alike (`process.env` is case-sensitive; config lookup is not).
11. The applicative builder rejects two sequential `let!` at compile time (a negative-compile test in the Guardrails/compile harness).
12. `withClock` after `withTimeZone` keeps the zone; `withTimeZone`/`withCulture` apply to forks (including `race`/`zipPar` branches and
    `disconnect`) and not outside, on .NET and on Fable after an `await`.
13. `runTest` reports `TimeZone.utc` and `Culture.invariant` regardless of the machine (the suite runs under `TZ=Pacific/Auckland`).
14. `TimeZone.offsetAt` for `Europe/Berlin`, `America/New_York`, `Australia/Lord_Howe` at a fixed table of instants around DST
    transitions is identical on .NET (Linux, Windows) and Fable/Node.
15. Guardrails: each new rule fires on its members and is silent on `CultureInfo.InvariantCulture` and `TimeZone.current`.
16. An AOT probe (`examples/Axial.AotProbe`) decodes a settings record and formats with a culture with no IL2xxx/IL3xxx warnings.

## 7. Benchmarks (quiet machine only)

| Benchmark | Question | Pass |
|---|---|---|
| FiberCost, Race, Pingpong/Threadring | does the larger `PlatformServices` change fork cost? | allocation identical (only the pointer is copied); time within noise |
| process start to first `runSync` | do lazy zone/culture defaults cost at startup? | within noise of 0.11.3; `TimeZoneInfo.Local` not touched when unused |
| `Flow.withClock` install | the bigger record copy at install | within noise |
| `TimeZone.current` read in a loop | runtime read cost | same as `Clock.now` read |
| `Config.decode` of a 30-key record, 4 sources | is decoding negligible at startup? | under 50 µs, no reflection in the profile |

## 8. Order of work after approval

1. T31 lands (Clock/Random/Guid/Log in core; only env vars left in `Axial.PlatformService`).
2. Invariant tests, failing (section 6); benchmark baselines recorded.
3. `TimeZone`, `Culture`, `PlatformServices` fields, `Flow.withTimeZone`/`withCulture`, `TestPlatform` defaults, Fable Intl backend.
4. `Axial.Configuration` project: move env-var code; `ConfigSource`, `Config`, `ConfigDecoder`, `Parse`, `Secret`, errors; JSON reader.
5. Hosts: `MicrosoftConfiguration.source` (replacing `Configuration`), `BrowserApp.queryParameters`, examples for .NET/Node/Generic Host.
6. Guardrails rules and messages; repo-wide Guardrails run at deny.
7. Deprecations, `0.12.0.md` rows, decisions README update (Platform now Clock/Guid/Random/TimeZone/Culture), docs set for Configuration (T33).

## 9. Alternatives considered

- **Reflection binding (`IConfiguration.Get<T>()` style).** Rejected: trimming/AOT warnings, no Fable, key names invisible, errors fail-fast.
- **Source-generated binders.** No F# source generators; a Myriad step would add a build tool for what ten lines of decoder do.
- **Configuration in the runtime context.** Rejected by the owner: authority stays in `'env`; it would be a hidden global.
- **Monadic CE with `Bind`.** Short-circuits on the first error, which is problem 1 in section 1.
- **`ITimeZone`/`ICulture` interfaces.** No behaviour to fake beyond choosing a value; interfaces invite stateful fakes.
- **Setting `CultureInfo.CurrentCulture` in `withCulture`.** Mutates ambient .NET state per fiber, leaks across `await`s on non-Axial code.

## 10. Open decisions — FOR OWNER REVIEW

Each has a recommendation; none is decided until the owner confirms.

- **C1. Capability name.** Recommend `IConfig` / `IHasConfig` (member `Config`); alternatives `ISettings`, or `IConfiguration` (clashes with MEL).
- **C2. Deprecate or delete `EnvironmentVariable` typed reads in 0.12.** Recommend `[<Obsolete>]` in 0.12, delete in 0.13.
- **C3. Precedence direction.** Recommend later-wins list order, matching `ConfigurationBuilder`.
- **C4. Decoder vs reflection.** Recommend the applicative decoder only (`config { let! .. and! .. }`, no `Bind`), plus `andThen` for dependent reads.
- **C5. JSON parser.** Recommend one hand-written reader for .NET and Fable; no `System.Text.Json` dependency, no comments or trailing commas.
- **C6. Boolean spellings.** Recommend `true`/`false` only (case-insensitive); `1/0/yes/no` via `Parse.oneOf` when wanted.
- **C7. Secrets.** Recommend explicit `Config.secret` + `Secret<'v>`; no key-name heuristics; snapshots never render values.
- **C8. File reading.** Recommend `Axial.Configuration` parses text only; the entry point reads the file through `Axial.FileSystem`.
- **C9. Reload.** Recommend none in 0.12: a snapshot at start. Revisit with a `Config.watch` stream only if asked.
- **C10. Zone and culture as sealed values, not interfaces.** Recommend values (Elm `Time.Zone`), wrapping `TimeZoneInfo`/`CultureInfo` on .NET and Intl on Fable.
- **C11. Local-to-UTC conversion.** Recommend deferring to its own design (typed `Ambiguous | Skipped`); T32 ships UTC-to-local only.
- **C12. `withCulture` and `CultureInfo.CurrentCulture`.** Recommend never setting the thread culture; `Culture.formatProvider` is explicit.
- **C13. Implicit-culture overloads in Guardrails.** Recommend not in 0.12 (CA1305 covers them); flag only ambient reads (4.3).
- **C14. Browser query string as a source.** Recommend yes, via `BrowserApp.queryParameters ()`; no other browser source.
