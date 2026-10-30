# Configuration, culture and time zone (T32)

Status: **owner decisions applied (2026-10-31)**. Decisions are listed in section 12, each one marked DECIDED or OPEN. Code starts
once the OPEN ones are answered or accepted as recommended. Breaking, so it ships in 0.12.0 with Reified 0.12.0.

Inputs: T30-T32 in `dev-docs/TASKS.md`, `capabilities.md` (binding), Axial at `859578a16`, and `~/projects/Reified-main`: `Data`,
`Data.ofCliArgs`, `Data.ofConfigurationPairs`, `Json.tryParseData`, `Schema.parse` (`Parsing.fs` turns `Number`/`Bool` into text before
parsing a field, so a string from any source decodes into a typed field), and `Data.merge`/`mergeAll`/`ofEnvironment`, new in
**Reified 0.12.0**. Guiding principle: closer to Elm (pure domain, effects as values, no exceptions or null, no hidden globals).

## 1. Problem

1. **Untyped and fail-fast.** `EnvironmentVariable.getInt "PORT"` returns one error and stops: ten bad settings, ten restarts.
2. **One source.** Arguments exist only on Node; `IConfiguration` is reached by pretending it is an environment
   (`Axial.Hosting.Configuration.environmentVariables`, whose `Set` throws).
3. **Culture leaks.** `getDouble` uses `AllowThousands` (`"1,5"` is `15`); user code reads `CultureInfo.CurrentCulture` and
   `TimeZoneInfo.Local`; implicit-culture overloads give machine-dependent text. Probe, .NET 10: `Double.Parse "1.5"` is `15` under
   `de-DE`; `"i".ToUpper()` is `"İ"` under `tr-TR`.

## 2. Packages in 0.12

- **`Axial.PlatformService` is deleted** (not deprecated). T31 moves `Clock`/`Random`/`Guid`/`Log` and `TestLog`/`TestPlatform`/
  `TestClock`/`runTest` into core `Axial`. Environment variables become a configuration source in `Axial.Configuration` (section 3.2);
  no env-var capability remains in a platform-services package.
- **`Axial.Configuration`** lives in the Axial repo, is released with Axial, and **references `Reified.Schema` and `Reified.Data`
  directly**, as `Axial.Hosting.AspNetCore` and `Axial.Hosting.GenHttp` already do. It references `Axial` and `Axial.FileSystem` (for
  `ConfigSource.jsonFile`). Targets `netstandard2.1;net8.0`, AOT- and Fable-compatible. It may be extracted later.
- **Core `Axial` keeps no Reified reference.** Culture and time zone (sections 4 and 5) are core and Reified-free.
- **Axial 0.12 requires Reified 0.12** (`merge`, `mergeAll`, `ofEnvironment`).
- Coordinator edits outside this doc: `dev-docs/TASKS.md` T16's acceptance rule becomes "core Axial has no Reified references;
  packages that need Reified data may"; the `AGENTS.md` invariant "Only the explicit HTTP host adapters may reference Reified packages"
  needs the same restatement.

## 3. `Axial.Configuration`

### 3.1 The typed model is a Reified Schema

The settings type is an ordinary record described by a `schema<_> { }`. Reified gives the explicit model, accumulated errors keyed by
`SchemaPath`, refinements, Fable and AOT. Axial adds no decoder and no parsers of its own.

```fsharp
open Reified.Schema
open Reified.SchemaDSL

type Db = { Host: NonBlankString; Port: int; Password: Secret<string> }
type AppSettings = { Db: Db; Timeout: int; Proxy: string option }

let dbSchema = schema<Db> { field _.Host; field _.Port { constrain (atLeast 1); constrain (atMost 65535) }; field _.Password; construct Db.create }
let settingsSchema = schema<AppSettings> { field _.Db { withSchema dbSchema }; field _.Timeout; field _.Proxy; construct AppSettings.create }
```

`"5432"` from an environment variable, `5432` from JSON and `--db.port 5432` all reach `Port` as text and parse with Reified's
invariant rules; `"1,5"` is an error on .NET and Fable alike. Defaults are a source (`ConfigSource.ofData "defaults" ...`), not a schema
feature, so they appear in precedence order like any other layer.

### 3.2 Sources produce `Data`; `Data.mergeAll` combines them

```fsharp
/// One named layer. Read is the effect; the result is plain Reified Data.
type ConfigSource<'env> =
    { Name: string                      // "env", "args", "appsettings.json"
      Spell: string list -> string      // how this source writes a path, for messages: ["Db";"Port"] -> "MYAPP__Db__Port"
      Read: Flow<'env, ConfigError, Data> }

module ConfigSource =
    val environment     : prefix: string option -> ConfigSource<'env>           // live process environment, Data.ofEnvironmentWith
    val environmentOf   : prefix: string option -> (string * string) seq -> ConfigSource<'env>  // same, from given pairs (tests)
    val commandLine     : args: string list -> ConfigSource<'env>               // Data.ofCliArgs; pure
    val jsonFile        : path: string -> ConfigSource<'env> when 'env :> IHasFileSystem   // missing file is SourceFailed
    val optionalJsonFile: path: string -> ConfigSource<'env> when 'env :> IHasFileSystem   // missing file is Data.Object []
    val json            : name: string -> text: string -> ConfigSource<'env>    // Json.tryParseData
    val queryString     : search: string -> ConfigSource<'env>                  // browser "?a=1&b.c=2", pure
    val ofData          : name: string -> Data -> ConfigSource<'env>            // defaults, fixtures
```

| Source | Built with | Notes |
|---|---|---|
| Environment | `Data.ofEnvironmentWith` (`__` separator, optional prefix stripped) | live read: `Environment.GetEnvironmentVariables()` / `process.env`, once |
| Command line, every host | `Data.ofCliArgs` | `--k=v`, `--k v`, `--flag`, `--no-flag`, `--`; repeats become a list; positionals under `_` |
| JSON file | `FileSystem.readAllText` then `Json.tryParseData` | the file read is visible as `IHasFileSystem` in the signature |
| `IConfiguration` | `Axial.Hosting`: `HostConfiguration.source name cfg` = `Data.ofConfigurationPairs (cfg.AsEnumerable())` | users keep appsettings.json, env, args, user-secrets |
| Browser query string | `BrowserApp.queryParameters ()` (new) into `ConfigSource.queryString` | the only browser source |

**Precedence is list order, later wins**: `Config.load schema [ defaults; file; env; args ]` reads every source, then
`Data.mergeAll` (right-biased deep merge: objects merge by field, lists and scalars replace, a later `Null` overrides). This matches
`ConfigurationBuilder`. There is no implicit source; an empty list is `Data.Object []`.

`Data.ofCliArgs` keeps `--Db:Port=5` as one flat key `"Db:Port"`; `ConfigSource.commandLine` re-nests `:`-separated names (the MEL
spelling) through `Data.ofConfigurationPairs` so they merge with the other layers (C13, OPEN).

### 3.3 Key case

Reified keeps names as written and matches schema fields exactly (`field _.Port` is `port`; an env var `MYAPP__DB__PORT` gives
`DB.PORT`). MEL is case-insensitive, so `Db:Port` in appsettings.json and `DB__PORT` in the environment are one key there and two keys
after `Data.mergeAll`. `Config.load` therefore canonicalises each source's names against the schema **before** merging: a name equal to
a schema field under ordinal-ignore-case is renamed to the field's canonical name (found with Reified `Inspect.model`); two different
spellings of one field inside one source are a `SourceFailed`; unknown names are left alone. Plain string reads (3.6) compare paths
ordinal-ignore-case. Alternative: a case-insensitive option in Reified's `SchemaParseOptions` (C12, OPEN).

### 3.4 The capability

```fsharp
namespace Axial.Configuration

/// The merged snapshot. Immutable; never reloaded.
type IConfig =
    abstract Data: Data
    abstract Sources: (string * (string list -> string)) list   // name and spelling, in precedence order, for messages

type IHasConfig =
    abstract Config: IConfig
```

- **Live**: `Config.loadData sources : Flow<'env, ConfigError list, IConfig>` at the entry point.
- **Fake**: `Config.ofData (data [ "Db" => [ "Host" => "localhost" ] ])`. Same type as live; there is no other fake.
- `Replaces` (T25b): `Environment.GetEnvironmentVariable(s)`, `GetCommandLineArgs`, `Microsoft.Extensions.Configuration` reads.

`IConfig` avoids the name `IConfiguration`, which MEL has open in every host file.

### 3.5 Load once, then pass the value

```fsharp
module Config =
    val load     : Schema<'model> -> ConfigSource<'env> list -> Flow<'env, ConfigError list, 'model>   // read, canonicalise, merge, parse
    val loadData : ConfigSource<'env> list -> Flow<'env, ConfigError list, IConfig>
    val parse    : Schema<'model> -> IConfig -> Result<'model, ConfigError list>                       // pure
    val section  : path: string -> Schema<'model> -> Flow<'env, ConfigError list, 'model> when 'env :> IHasConfig
    val ofData   : Data -> IConfig
```

Configuration is loaded **once at startup into a plain value**, Elm-flags style. The typed record, not `IConfig`, goes where it is
used, and every later consumer captures it:

- **Flows**: put `settings` (or the part a service needs) in the `'env` record; read it with `Flow.envWith _.Settings`.
- **ASP.NET/GenHttp handlers and middleware**: the handler is a closure built after loading, `app.MapGet("/", handler settings)`, or a
  middleware constructed with the value. No per-request configuration lookup, no `IOptions<T>` resolution.
- **Elmish**: `Program.mkProgram (init settings) (update settings) view`; the value is an argument, like Elm's `flags`.
- **Callbacks** (timers, `Process` output handlers, UI events): capture the value in the closure that is registered.

Nothing reloads (an app that needs `IOptionsMonitor` keeps it on the MEL side). Every consumer sees one snapshot, and a test passes a
record literal. `IHasConfig` and `Config.section` are for libraries that own a section (`Config.section "Http" HttpSettings.schema`).

### 3.6 Plain string reads

For a tool with two settings a schema is ceremony. Two reads over `IHasConfig`, plus a pure form:

```fsharp
module Config =
    val required : path: string -> Flow<'env, ConfigError, string> when 'env :> IHasConfig          // absent -> Missing
    val optional : path: string -> Flow<'env, ConfigError, string option> when 'env :> IHasConfig   // absent -> None
    val tryFind  : path: string -> IConfig -> Result<string option, ConfigError>                     // pure
```

Paths use Reified's `DataPath` syntax (`"Db.Port"`). `Number`/`Bool` leaves come back as their text; an object or list is `NotText`.

### 3.7 Errors

```fsharp
type ConfigError =
    | SourceFailed of source: string * message: string   // unreadable file, bad JSON (Json.tryParseData's message), duplicate spelling
    | Invalid of SchemaErrors                            // every schema issue from one parse, keyed by SchemaPath
    | Missing of path: string                            // plain read
    | NotText of path: string                            // plain read

module ConfigError =
    val describe : sources: IConfig -> ConfigError -> string     // invariant culture; one line per issue, sorted by path
```

`Config.load` reads every source and reports **all** `SourceFailed` errors; it parses only when every source was read, so a broken file
does not also produce a page of misleading `Omitted` issues. `describe` appends where a missing path could come from, using each
source's `Spell`: `Db.Host was omitted (set one of: --Db:Host, MYAPP__Db__Host, appsettings.json Db.Host)`. Nothing throws.

### 3.8 Secrets

A secret is declared, never guessed from key names: the field's type is `Secret<'v>` (sealed; `ToString()` is `"<redacted>"`; no
structural equality; read with `Secret.reveal`; its schema replaces any issue under it with a value-free one, so `SchemaErrors`,
`describe`, `%A`/`%O` and log entries never contain the value). `IConfig` and `describe` print paths and source names, never values.

Where `Secret` lives (C7a, OPEN). **Recommendation: Reified (`Reified.Refinements`, canonical `Schema` member).** Redaction has to happen
where the error is produced, inside `Schema.parse` and the refinement constructor; Axial only sees the finished `SchemaErrors`. In
Reified it also covers HTTP bodies and JSON encoding (a `Secret` field encodes as an error or as redacted, never as plain text). If it
cannot make Reified 0.12, `Axial.Configuration` ships it with `Schema.refine` and the same contract, and moves it later.

### 3.9 Hosts

| Host | Arguments | Environment | Files / other |
|---|---|---|---|
| .NET console (`App.run`) | `main argv` into `ConfigSource.commandLine` | `ConfigSource.environment` | `ConfigSource.jsonFile` |
| Generic Host / ASP.NET Core | already in `IConfiguration` (`CreateDefaultBuilder(args)`) | same | `HostConfiguration.source "host" builder.Configuration` |
| Node | `NodeApp.arguments ()` | `ConfigSource.environment` (`process.env`) | `ConfigSource.jsonFile` (Node file system) |
| Browser | `BrowserApp.queryParameters ()` | none | JSON embedded by the build, or fetched, into `ConfigSource.json` |

No host calls `Environment.GetCommandLineArgs()`; arguments arrive as a value. Hosts do not install configuration in the runtime
context. Tests call `Config.parse settingsSchema (Config.ofData (data [ ... ]))` with no flow and build sources from strings and pairs
(`json`, `environmentOf`, `commandLine`); `ConfigSource.environment` reads the process even under `runTest`.

## 4. Culture: an explicit value

### 4.1 In the runtime's `PlatformServices`

```fsharp
namespace Axial

[<Sealed>] type Culture   // .NET: a read-only CultureInfo; Fable: a BCP 47 name used with Intl. "" = invariant.

module Culture =
    val invariant : Culture
    val ofName    : string -> Result<Culture, string>      // .NET: CultureInfo.GetCultureInfo (cached, read-only); Fable: Intl.getCanonicalLocales
    val name      : Culture -> string
    val current   : Flow<'env, 'error, Culture>            // reads the runtime context
#if !FABLE_COMPILER
    val formatProvider : Culture -> IFormatProvider        // for APIs that take a provider
    val ambient   : Culture -> (unit -> 'a) -> 'a          // 4.4
#endif

module Flow =
    val withCulture : Culture -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>   // scope: the flow and everything it forks
```

Live default: `PlatformDefaults` captures the process's `CultureInfo.CurrentCulture` instance once, lazily. `runTest`: invariant
(`TestPlatform.Culture`, settable). A culture is a value, not an interface: its only fake is another value.

### 4.2 Guardrails bans implicit-culture forms

Banned everywhere except inside `Culture.ambient` (4.4) and listed boundary files. Verified by a probe on .NET 10 under `de-DE`/`tr-TR`.

| Banned form | Example of the failure | Use instead | Rule |
|---|---|---|---|
| Untyped interpolation hole of a number/date: `$"{x}"`, `$"{x:N2}"` | `$"{1.5}"` is `"1,5"` under de-DE | typed hole `$"%f{x}"`, `string x` | F# rule `culture.interpolation-hole` |
| `ToString()` / `ToString(format)` without provider on `IFormattable` | `(1.5).ToString()` is `"1,5"` | `string x`, `ToString(fmt, Culture.formatProvider c)` | CA1305 |
| `String.Format`, `StringBuilder.AppendFormat`, `Console.Write(format, ..)` without provider | culture-formatted args | `sprintf`, provider overload | CA1305 |
| `String.Concat(obj)` with an `IFormattable` argument | as `ToString()` | `string x` | F# rule `culture.object-format` (no provider overload, so CA1305 misses it) |
| `%O` on a number or date | `sprintf "%O" 1.5` is `"1,5"` | `%f`, `%d`, `%s` with `string` | F# rule `culture.object-format` |
| `Parse`/`TryParse`/`Convert.ToX(string)` without provider | `Double.Parse "1.5"` is `15` | `Double.Parse(s, CultureInfo.InvariantCulture)`, Reified schema | CA1305 |
| `ToUpper()`/`ToLower()` without culture | `"i".ToUpper()` is `"İ"` under tr-TR | `ToUpperInvariant()`, `ToUpper(culture)` | CA1304, CA1311 |
| `String.Compare`, `StartsWith`/`EndsWith`/`IndexOf(string)` without `StringComparison`; `CurrentCulture*` comparisons | culture-dependent order and matches | `StringComparison.Ordinal`/`OrdinalIgnoreCase` | CA1307, CA1309, CA1310 |
| Reads or writes of `CultureInfo.CurrentCulture`/`CurrentUICulture`, `Thread.CurrentCulture`, `DefaultThreadCurrentCulture` | ambient global | `Culture.current`, `Flow.withCulture` | existing symbol rule `culture.current` |

Allowed (already invariant or ordinal in F#): `string x`, `sprintf` `%f`/`%d`/`%s`, typed interpolation holes, `float`/`int`
conversions of strings, F# `compare`, `List.sort`.

Roslyn analyzers (the CA rules) do not run on F# projects: the F# compiler does not host them. Guardrails therefore implements each CA
rule above as an FSharp.Analyzers typed-tree rule that carries the CA id in its code and message (the typed tree has the resolved
overload, which the old design said was missing), and its `buildTransitive` props enable the real CA rules for C# projects in the same
solution (C17, OPEN on this split).

### 4.3 Interop boundary: the flow's culture reaches C# code

Code Guardrails cannot see (C# libraries, other assemblies) reads `CultureInfo.CurrentCulture`. When a flow invokes a
`ColdTask`/`Task`/`ValueTask`/`Async` factory, Axial makes the flow's culture the thread's for the duration of the factory call:

1. Before invoking: if `CultureInfo.CurrentCulture` is not reference-equal to the flow culture's `CultureInfo`, set it (and
   `CurrentUICulture` likewise), remembering the previous values.
2. Invoke the factory. `CurrentCulture` is backed by an `AsyncLocal`, so the `ExecutionContext` the task captures at each `await`
   carries the flow's culture into its continuations.
3. Restore the previous values in a `finally` as soon as the factory returns.

The default path costs one reference comparison per call: the live default is the captured process culture instance, so it is equal
unless the flow used `withCulture` (or runs under `runTest` on a non-invariant machine). This lives in the same internal user-code
invocation chokepoint as T30's exception wrapper: one call site per interop kind, one place to benchmark (benchmark 3). Only culture
is synced; time zone cannot be (section 5). The ASP.NET adapter may adopt the request's RequestLocalization culture
(`IRequestCultureFeature`) as the request flow's culture (C19, OPEN on default vs opt-in).

### 4.4 `Culture.ambient` for direct synchronous C# calls

```fsharp
let! culture = Culture.current
let text = Culture.ambient culture (fun () -> Legacy.Report.Render model)   // sets and restores CurrentCulture/CurrentUICulture
```

.NET only, synchronous only (a `Task` started inside escapes the restore; use the interop factories of 4.3 for those). It exists for
direct C# calls outside task factories. Guardrails allows the banned forms of 4.2 only lexically inside its lambda.

**Fable.** No ambient culture: formatting is invariant JS, the allowed forms agree with .NET (invariant test 7), and `Culture` is used
explicitly with `Intl` (`Intl.NumberFormat(Culture.name c)`). `Culture.ambient` and the interop sync are .NET only.

## 5. Time zone: an explicit value

```fsharp
[<Sealed>] type TimeZone   // .NET: TimeZoneInfo; Fable: IANA id plus Intl.DateTimeFormat for offsets

module TimeZone =
    val utc         : TimeZone
    val fixedOffset : TimeSpan -> TimeZone
    val ofId        : string -> Result<TimeZone, string>        // IANA ids on every target; Windows ids also on .NET
    val id          : TimeZone -> string
    val offsetAt    : TimeZone -> DateTimeOffset -> TimeSpan    // pure
    val toLocal     : TimeZone -> DateTimeOffset -> DateTimeOffset
    val current     : Flow<'env, 'error, TimeZone>
#if !FABLE_COMPILER
    val ofTimeZoneInfo : TimeZoneInfo -> TimeZone
    val toTimeZoneInfo : TimeZone -> TimeZoneInfo               // to pass a zone to C# APIs explicitly
#endif

module Clock = val nowLocal : Flow<'env, 'error, DateTimeOffset>   // Clock.now |> TimeZone.toLocal current
module Flow  = val withTimeZone : TimeZone -> Flow<'env,'error,'value> -> Flow<'env,'error,'value>
```

`TimeZoneInfo.Local` is process-wide, so a zone cannot be scoped ambiently per flow: there is no interop sync and no `ambient` block.
C# code that reads `TimeZoneInfo.Local` sees the machine zone; pass `TimeZone.toTimeZoneInfo` explicitly. Live default:
`TimeZoneInfo.Local` (.NET) or `Intl.DateTimeFormat().resolvedOptions().timeZone` (Fable), read lazily once. `runTest`: UTC.
Fable offsets come from `formatToParts` with `hourCycle: "h23"`; an invalid id's `RangeError` becomes `Error`; Node without full ICU
returns `Error` for non-UTC ids (documented, tested on the CI Node version).
T32 ships UTC-to-local only; local-to-UTC (skipped and repeated hours) needs a typed `Ambiguous | Skipped` result and its own design.

## 6. Runtime fields and `withClock`

```fsharp
type internal PlatformServices =
    { Clock: IClock; Random: IRandom; Guid: IGuid; Time: Platform.ITimeSource
      TimeZone: TimeZone
      Culture: Culture }
```

`RuntimeContext` holds `Platform` as one pointer, so fork/branch/disconnect copies do not grow; the record grows from four to six
fields, paid only at `Flow.with*` install. `withClock` today rebuilds with `PlatformServices.Create(clock, Random, Guid)`, which would
drop the zone and culture. It changes to `{ runtime.Platform with Clock = clock; Time = Platform.timeOfClock clock }` (invariant test 7).
`TestPlatform` gains `TimeZone` (UTC) and `Culture` (invariant); `trace` records `TimeZone.current` and `Culture.current` reads.

## 7. Guardrails rules beyond culture

| Rule | Members | Suggestion |
|---|---|---|
| `clock.local-timezone` (exists; message changes) | `TimeZoneInfo.Local` | `TimeZone.current`, `Flow.withTimeZone` |
| `clock.datetime` (exists; message adds) | `DateTime.Now`, `Today` | `Clock.nowLocal` |
| `clock.to-local` (new) | `DateTime.ToLocalTime`, `DateTimeOffset.ToLocalTime`, `TimeZoneInfo.ConvertTime*` with `Local` | `TimeZone.toLocal` |
| `environment.state` (exists; adds) | `Environment.GetCommandLineArgs`, `Environment.CommandLine`, `GetEnvironmentVariable(s)` | `ConfigSource.commandLine`/`environment` |

The rules live under the guardrail of the package that provides the replacement: culture and time-zone rules under core `Axial`,
environment and argument rules under `Axial.Configuration`. Axial's own `src` runs them at deny; the exceptions are `PlatformDefaults`,
the interop chokepoint, `Culture.ambient` and `ConfigSource.environment`, each with an `axial-allow` reason.

## 8. Migration from 0.11.3

| 0.11.3 | 0.12 |
|---|---|
| package `Axial.PlatformService` | deleted; Clock/Random/Guid/Log/runTest in `Axial` (T31), configuration in `Axial.Configuration` |
| `IEnvironmentVariables`, `EnvironmentVariables.*` (`tryGet`, `set`, `expand`, `live`, `fromPairs`, `layer`) | deleted; `ConfigSource.environment` / `environmentOf`; child-process variables are passed to `Axial.Process` explicitly |
| `EnvironmentVariable.get "X"` / `tryGet` | `Config.required "X"` / `Config.optional "X"` |
| `EnvironmentVariable.getInt` / `getDouble` / ... | a schema field; `Config.load` reports every error |
| `EnvironmentVariableError` (one, fail-fast) | `ConfigError list`; `ConfigError.describe` |
| `Axial.Hosting.Configuration.environmentVariables cfg` / `.layer` | `HostConfiguration.source "host" cfg` in the source list |
| `NodeApp.arguments ()` | unchanged; feed it to `ConfigSource.commandLine` |
| `TimeZoneInfo.Local`, `DateTime.Now`, `CultureInfo.CurrentCulture`, implicit-culture overloads | `TimeZone.current`, `Clock.nowLocal`, `Culture.current`, the replacements in 4.2 |

The env-var typed reads are deleted in 0.12, not obsoleted (pre-1.0 rule). Rows go in `dev-docs/releases/0.12.0.md`.

## 9. Invariant tests (written first; each must fail before implementation)

1. Three bad fields across two sources: one `Invalid` with exactly three issues. Two unreadable sources: two `SourceFailed`, no `Invalid`.
2. Precedence: a key in env and args yields the args value; reversing the list reverses the winner; a later `Null` overrides.
3. `describe` for an omitted path lists every source's spelling (`--Db:Host`, `MYAPP__Db__Host`, `appsettings.json Db.Host`).
4. Keys: `Db.Port` (JSON), `MYAPP__DB__PORT` (env) and `--Db:Port=5` (args) merge into one field; two spellings in one source are
   `SourceFailed`; positionals arrive under `_`.
5. A secret's value never appears in `SchemaErrors`, `describe`, `ToString`, `%A`, `%O`, or a log entry built from the error.
6. `"5432"`, `5432` and `"true"` decode the same from env, JSON and args; `"1,5"` is rejected; run under `de-DE` and `tr-TR` thread
   cultures on .NET and as the same table on Fable.
7. Culture: invariant under `runTest` on a `de-DE` machine; `withCulture` applies to forks (`race`, `zipPar`, `disconnect`) and not
   outside; the allowed forms give identical text on .NET and Fable; `withClock` after `withCulture`/`withTimeZone` keeps both.
8. Interop sync: a C# helper that awaits and then formats `1.5` returns `"1,5"` inside `withCulture de` via `ofTask`, `ofColdTask`,
   `ofValueTask`, `ofAsync`; the thread's culture is restored afterwards; with the default culture the setter is never called (a
   counting test hook); `Culture.ambient` restores after a throw.
9. Time zone: `runTest` reports UTC under `TZ=Pacific/Auckland`; `withTimeZone` scopes like `withCulture`; `offsetAt` for
   `Europe/Berlin`, `America/New_York`, `Australia/Lord_Howe` around DST transitions is identical on .NET (Linux, Windows) and Node.
10. Guardrails: each row of 4.2 and 7 fires on a positive sample and is silent on its replacement and inside `Culture.ambient`.
11. An AOT probe loads configuration through a schema and formats with a culture with no IL2xxx/IL3xxx warnings.

## 10. Benchmarks (quiet machine only)

| Benchmark | Question | Pass |
|---|---|---|
| FiberCost, Race, Pingpong/Threadring | does the six-field `PlatformServices` change fork cost? | allocation identical; time within noise |
| process start to first `runSync` | do lazy zone/culture defaults cost at startup? | within noise of 0.11.3 |
| `Flow.ofTask` / `ofValueTask` loop, default culture | cost of the interop reference check in the T30 chokepoint | within noise of the T30 baseline, no allocation |
| same loop under `withCulture` | cost of set and restore | recorded; under 50 ns per call |
| `Culture.current` / `TimeZone.current` read loop | runtime read cost | same as `Clock.now` |
| `Config.load` of a 30-field schema, 4 sources | is loading negligible at startup? | under 200 µs, no reflection in the profile |

## 11. Order of work

1. **Reified 0.12.0 release** (`Data.merge`, `mergeAll`, `ofEnvironment`; `Secret` if C7a is accepted; case option if C12 picks it).
2. **T31**: Clock/Random/Guid/Log and the test platform move to core `Axial`; `Axial.PlatformService` deleted.
3. **Culture and time zone in `PlatformServices`**: types, `withCulture`/`withTimeZone`, `withClock` fix, `TestPlatform` defaults, Fable
   Intl backend, then the interop culture sync inside T30's chokepoint, with benchmarks 1-5 before and after.
4. **`Axial.Configuration`**: sources, canonicalisation, `Config.load`/`loadData`/plain reads, errors; `HostConfiguration.source` in
   `Axial.Hosting`; `BrowserApp.queryParameters`; examples for .NET, Node, Generic Host.
5. **Guardrails rules** (4.2, 7) and the repo-wide run at deny.
6. **Docs set** for Configuration and the culture/time-zone pages (T33), `0.12.0.md` rows, decisions README (Platform is
   Clock/Guid/Random/TimeZone/Culture).

## 12. Decisions

DECIDED means owner, 2026-10-31.

| # | Decision | Status |
|---|---|---|
| C1 | Typed model is a Reified `Schema`; Axial's own decoder (old C4) and parser table dropped | DECIDED |
| C2 | Every source produces Reified `Data`; combined with `Data.mergeAll`, later wins | DECIDED |
| C3 | JSON via `Json.tryParseData`, files via `Axial.FileSystem` (`ConfigSource.jsonFile`); own reader (old C5) dropped | DECIDED |
| C4 | Plain string reads `Config.required` / `optional` / `tryFind` | DECIDED |
| C5 | Loaded once at startup into a plain value, captured by consumers; no reload | DECIDED |
| C6 | Capability `IConfig` / `IHasConfig` (member `Config`); fake `Config.ofData` | DECIDED |
| C7 | Explicit `Secret<'v>`, no key-name heuristics, values never rendered | DECIDED |
| C7a | Home of `Secret`: recommend `Reified.Refinements` (3.8); fallback `Axial.Configuration` | OPEN |
| C8 | `Axial.Configuration` in the Axial repo references Reified; core `Axial` does not; Axial 0.12 requires Reified 0.12 | DECIDED |
| C9 | `Axial.PlatformService` deleted in 0.12; env vars become a source; env-var typed reads deleted (old C2) | DECIDED |
| C10 | Sources: env, command line on every host, JSON files, `IConfiguration` adapter, browser query string (old C14) | DECIDED |
| C11 | Scalar parsing is Reified's (old C6 boolean spellings defer to `Schema.bool`) | DECIDED |
| C12 | Key case: recommend canonicalising names against the schema before merge (3.3); alternative a Reified parse option | OPEN |
| C13 | CLI nesting: recommend `commandLine` re-nests `:` names in 0.12, moving into a later `Data.ofCliArgsWith` | OPEN |
| C14 | Culture is an explicit value in `PlatformServices`; `Culture.current`, `withCulture`, invariant under `runTest` | DECIDED |
| C15 | Zone and culture are sealed values, not interfaces (old C10) | DECIDED |
| C16 | Guardrails bans the implicit-culture forms of table 4.2 (supersedes old C13) | DECIDED |
| C17 | CA rules on F#: recommend F# typed-tree rules carrying the CA ids, plus the real CA rules for C# projects | OPEN |
| C18 | Interop culture sync in the T30 chokepoint; `Culture.ambient` for direct synchronous calls (supersedes old C12) | DECIDED |
| C19 | ASP.NET request culture: recommend adopting `IRequestCultureFeature` when the app called `UseRequestLocalization` | OPEN |
| C20 | Time zone is an explicit value in `PlatformServices`; `TimeZone.current`, `withTimeZone`, UTC under `runTest` | DECIDED |
| C21 | Local-to-UTC deferred; T32 ships UTC-to-local only (old C11) | DECIDED |
| C22 | `withClock` record update keeps zone and culture; invariant test 7 | DECIDED |
