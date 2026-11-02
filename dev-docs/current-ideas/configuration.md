# Configuration, culture and time zone (T32)

Status: **DECIDED (owner, 2026-10-31)**. Decisions are listed in section 12. Breaking, so it ships in 0.12.0 with Reified 0.12.0.

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
- **Axial 0.12 requires Reified 0.12** (`Data.merge`, `mergeAll`, `ofEnvironment`, case-insensitive key parse option, nested CLI args,
  and `Secret` in Refinements).
- **`Axial.Files` merges into `Axial.FileSystem`.** Core stays Reified-free; packages that need Reified data may reference it.

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
    val environment     : prefix: string option -> ConfigSource<'env>           // live process environment, Data.ofEnvironment
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
| Environment | `Data.ofEnvironment` (with prefix support; `__` separator, optional prefix stripped) | live read: `Environment.GetEnvironmentVariables()` / `process.env`, once |
| Command line, every host | `Data.ofCliArgs` | `--k=v`, `--k v`, `--flag`, `--no-flag`, `--`; repeats become a list; positionals under `_` |
| JSON file | `FileSystem.readAllText` then `Json.tryParseData` | the file read is visible as `IHasFileSystem` in the signature |
| `IConfiguration` | `Axial.Hosting`: `HostConfiguration.source name cfg` = `Data.ofConfigurationPairs (cfg.AsEnumerable())` | users keep appsettings.json, env, args, user-secrets |
| Browser query string | `BrowserApp.queryParameters ()` (new) into `ConfigSource.queryString` | the only browser source |

**Precedence is list order, later wins**: `Config.load schema [ defaults; file; env; args ]` reads every source, then
`Data.mergeAll` (right-biased deep merge: objects merge by field, lists and scalars replace, a later `Null` overrides). This matches
`ConfigurationBuilder`. There is no implicit source; an empty list is `Data.Object []`.

**DECIDED (owner, 2026-10-31):** Reified 0.12's `Data.ofCliArgs` handles nested `--Db:Port=5` directly; Axial does not re-nest keys.

### 3.3 Key case

**DECIDED (owner, 2026-10-31):** keys match case-insensitively through a Reified parse option, not Axial schema inspection or
canonicalisation. `Db.Port`, `DB__PORT` and `--Db:Port` address the same field across merged sources, with later sources winning.
Plain string reads (3.6) also compare paths ordinal-ignore-case. Reified owns the merge/parse interaction needed for this precedence.

### 3.4 The capability

```fsharp
namespace Axial.Configuration

/// One immutable merged snapshot.
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
    val load     : Schema<'model> -> ConfigSource<'env> list -> Flow<'env, ConfigError list, 'model>   // read, merge, parse with case-insensitive keys
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

`Config.load` remains the default; a test passes a record literal. `IHasConfig` and `Config.section` are for libraries that own a
section (`Config.section "Http" HttpSettings.schema`).

**DECIDED (owner, 2026-10-31):** optional `Config.watch sources schema` ships in 0.12 and returns a `SubscriptionRef` of the latest
valid configuration. Build it from `Axial.FileSystem` watching file sources, `FlowStream.debounce`, then re-read, merge and decode.
Environment and arguments remain static. Publish only successful decodes: the last good value stays on failure; decode failures are
logged and exposed as an error stream. Consumers opt into observing changes rather than silently replacing captured startup values.

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
    | SourceFailed of source: string * message: string   // unreadable file, bad JSON (Json.tryParseData's message)
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

**DECIDED (owner, 2026-10-31):** `Secret<'v>` lives in Reified (`Reified.Refinements`, canonical `Schema` member), required in 0.12.
Redaction happens where errors are produced, inside `Schema.parse` and the refinement constructor; Axial sees the finished
`SchemaErrors`. This also covers HTTP bodies and JSON encoding (a secret encodes as an error or redacted, never as plain text).

**DECIDED (owner, 2026-10-31):** `Axial.Process` keeps its own argument-level redaction (`Process.secret`, `Process.secretArg`:
the real value goes to the child, `***` in display, transcripts and failures). `Axial.Configuration` adds the bridge
`Config.processArg : Secret<'v> -> ProcessArgument`, which yields a `Process.secret` argument without the raw value ever appearing in
user code, so a config secret cannot reach a transcript by forgetting to re-wrap `Secret.reveal`. `Axial.Process` gains no Reified
reference. Test: a command built with `Config.processArg` shows `***` in its plan, transcript and failure text.

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

### 4.1 Read an effect, keep a value

**DECIDED (owner, 2026-10-31):** culture and time zone are plain sealed values, not runtime-context fields. Apps keep them in the
model or pass them as parameters, like Elm's `Time.here` result.

```fsharp
namespace Axial

[<Sealed>] type Culture   // .NET: a read-only CultureInfo; Fable: a BCP 47 name used with Intl. "" = invariant.

module Culture =
    val invariant : Culture
    val ofName    : string -> Result<Culture, string>      // .NET: CultureInfo.GetCultureInfo (cached, read-only); Fable: Intl.getCanonicalLocales
    val name      : Culture -> string
    val system : Flow<'env, 'error, Culture>
    val setProcessDefault : Culture -> Flow<'env, 'error, unit>

module Format =
    val number   : Culture -> float -> string
    val currency : Culture -> decimal -> string
    val date     : Culture -> DateTimeOffset -> string
    val time     : Culture -> DateTimeOffset -> string
```

`Culture.system` reads the machine default explicitly, like `Clock.now`; `TestPlatform` supplies invariant under `runTest`.
On Avalonia browser/Fable it reads `navigator.language`. Pure `Format.number/currency/date/time` take a culture value, using .NET
formatting or Fable `Intl`. On .NET `Culture` implements `IFormatProvider`, so `x.ToString("C", culture)` works.

### 4.2 Guardrails bans implicit-culture forms

Banned except in justified live effect boundaries; only `Culture.setProcessDefault` may set ambient culture. Verified by a probe on .NET 10 under `de-DE`/`tr-TR`.

| Banned form | Example of the failure | Use instead | Rule |
|---|---|---|---|
| Untyped interpolation hole of a number/date: `$"{x}"`, `$"{x:N2}"` | `$"{1.5}"` is `"1,5"` under de-DE | typed hole `$"%f{x}"`, `string x` | F# rule `culture.interpolation-hole` |
| `ToString()` / `ToString(format)` without provider on `IFormattable` | `(1.5).ToString()` is `"1,5"` | `string x`, `ToString(fmt, culture)` | CA1305 |
| `String.Format`, `StringBuilder.AppendFormat`, `Console.Write(format, ..)` without provider | culture-formatted args | `sprintf`, provider overload | CA1305 |
| `String.Concat(obj)` with an `IFormattable` argument | as `ToString()` | `string x` | F# rule `culture.object-format` (no provider overload, so CA1305 misses it) |
| `%O` on a number or date | `sprintf "%O" 1.5` is `"1,5"` | `%f`, `%d`, `%s` with `string` | F# rule `culture.object-format` |
| `Parse`/`TryParse`/`Convert.ToX(string)` without provider | `Double.Parse "1.5"` is `15` | `Double.Parse(s, CultureInfo.InvariantCulture)`, Reified schema | CA1305 |
| `ToUpper()`/`ToLower()` without culture | `"i".ToUpper()` is `"İ"` under tr-TR | `ToUpperInvariant()`, `ToUpper(culture)` | CA1304, CA1311 |
| `String.Compare`, `StartsWith`/`EndsWith`/`IndexOf(string)` without `StringComparison`; `CurrentCulture*` comparisons | culture-dependent order and matches | `StringComparison.Ordinal`/`OrdinalIgnoreCase` | CA1307, CA1309, CA1310 |
| Reads or writes of `CultureInfo.CurrentCulture`/`CurrentUICulture`, `Thread.CurrentCulture`, `DefaultThreadCurrentCulture`/`DefaultThreadCurrentUICulture` | ambient global | `Culture.system`; only `Culture.setProcessDefault` may set them | existing symbol rule `culture.current` |

Allowed (already invariant or ordinal in F#): `string x`, `sprintf` `%f`/`%d`/`%s`, typed interpolation holes, `float`/`int`
conversions of strings, F# `compare`, `List.sort`.

Roslyn analyzers (the CA rules) do not run on F# projects: the F# compiler does not host them. Guardrails therefore implements each CA
rule above as an FSharp.Analyzers typed-tree rule that carries the CA id in its code and message (the typed tree has the resolved
overload, which the old design said was missing), and its `buildTransitive` props enable the real CA rules for C# projects in the same
solution. **DECIDED (owner, 2026-10-31), C17.**

### 4.3 Process startup and host guidance

**DECIDED (owner, 2026-10-31):** `Culture.setProcessDefault` is an explicit effect, called once at desktop/browser startup, not
inside `App`. On .NET it sets `CultureInfo.DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`, `CurrentCulture` and
`CurrentUICulture` so third-party controls and libraries follow. It is a no-op on Fable. No interop-boundary synchronization occurs.

| App type | Guidance |
|---|---|
| Desktop | Read once at startup (or use saved preference), call `Culture.setProcessDefault`, keep the value in the model |
| Avalonia browser | Same startup/model pattern; Fable uses `navigator.language`; verify .NET WASM culture data is loaded, since builds may be invariant by default |
| API server | Invariant machine data; recommend `<InvariantGlobalization>true</InvariantGlobalization>` |
| Server-rendered pages | Per request, the ASP.NET adapter automatically adopts the request culture from RequestLocalization when `UseRequestLocalization` is used, and passes it to the handler as a value |

Request handling does not set a process default per request. Pure code receives the culture explicitly.

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
    val local       : Flow<'env, 'error, TimeZone>             // explicit machine-default read; UTC under runTest
#if !FABLE_COMPILER
    val ofTimeZoneInfo : TimeZoneInfo -> TimeZone
    val toTimeZoneInfo : TimeZone -> TimeZoneInfo               // to pass a zone to C# APIs explicitly
#endif

```

**DECIDED (owner, 2026-10-31):** `TimeZone.local` explicitly reads `TimeZoneInfo.Local` (.NET) or
`Intl.DateTimeFormat().resolvedOptions().timeZone` (Fable). `TestPlatform` supplies UTC under `runTest`. Keep the result in the model
or pass it to pure `TimeZone.toLocal zone instant`; pass `TimeZone.toTimeZoneInfo` to C# APIs explicitly.
Stored and passed times are UTC; local conversion is for presentation.
Fable offsets come from `formatToParts` with `hourCycle: "h23"`; an invalid id's `RangeError` becomes `Error`; Node without full ICU
returns `Error` for non-UTC ids (documented, tested on the CI Node version).
T32 ships UTC-to-local only; local-to-UTC (skipped and repeated hours) needs a typed `Ambiguous | Skipped` result and its own design.

## 6. Runtime boundary

**DECIDED (owner, 2026-10-31, amended):** no culture or zone *value* is stored or scoped in the runtime context: there is
no `withCulture`/`withTimeZone`/`Culture.current`, and application code passes the values explicitly. Only the machine-default
*readers* behind `Culture.system`/`TimeZone.local` sit in `PlatformServices`, exactly like `Clock`, so that every machine read is a
platform effect that `runTest` makes deterministic (invariant, UTC). They are overridden only by the internal test path; there is no
public API to replace them. This matches ZIO/Effect-TS test services. Drop the proposed `withClock`
record-update fix and preservation test: they existed only to retain the now-removed fields.

## 7. Guardrails rules beyond culture

| Rule | Members | Suggestion |
|---|---|---|
| `clock.local-timezone` (exists; message changes) | `TimeZoneInfo.Local` | `TimeZone.local` |
| `clock.datetime` (exists; message adds) | `DateTime.Now`, `Today` | `Clock.now`, then pure `TimeZone.toLocal zone instant` |
| `clock.unspecified` | `DateTimeKind.Unspecified` constructors | store/pass UTC instants |
| `clock.to-local` (new) | `DateTime.ToLocalTime`, `DateTimeOffset.ToLocalTime`, `TimeZoneInfo.ConvertTime*` with `Local` | `TimeZone.toLocal` |
| `environment.state` (exists; adds) | `Environment.GetCommandLineArgs`, `Environment.CommandLine`, `GetEnvironmentVariable(s)` | `ConfigSource.commandLine`/`environment` |

The rules live under the guardrail of the package that provides the replacement: culture and time-zone rules under core `Axial`,
environment and argument rules under `Axial.Configuration`. Axial's own `src` runs them at deny; named live read boundaries,
`Culture.setProcessDefault` and `ConfigSource.environment` require rule-specific `axial-allow` reasons. Machine data is invariant.

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
| `TimeZoneInfo.Local`, `DateTime.Now`, `CultureInfo.CurrentCulture`, implicit-culture overloads | `TimeZone.local`, `Clock.now`, `Culture.system`, explicit pure formatting/conversion and the replacements in 4.2 |

The env-var typed reads are deleted in 0.12, not obsoleted (pre-1.0 rule). Rows go in `dev-docs/releases/0.12.0.md`.

## 9. Invariant tests (written first; each must fail before implementation)

1. Three bad fields across two sources: one `Invalid` with exactly three issues. Two unreadable sources: two `SourceFailed`, no `Invalid`.
2. Precedence: a key in env and args yields the args value; reversing the list reverses the winner; a later `Null` overrides.
3. `describe` for an omitted path lists every source's spelling (`--Db:Host`, `MYAPP__Db__Host`, `appsettings.json Db.Host`).
4. Keys: `Db.Port` (JSON), `MYAPP__DB__PORT` (env) and `--Db:Port=5` (args) merge into one field using the Reified key option;
   later sources win despite case differences; nested CLI args are handled by Reified; positionals arrive under `_`.
5. A secret's value never appears in `SchemaErrors`, `describe`, `ToString`, `%A`, `%O`, or a log entry built from the error.
6. `"5432"`, `5432` and `"true"` decode the same from env, JSON and args; `"1,5"` is rejected; run under `de-DE` and `tr-TR` thread
   cultures on .NET and as the same table on Fable.
7. `Culture.system` returns invariant under `runTest` on a `de-DE` machine; browser/Fable reads `navigator.language` live.
   Pure formatting uses the supplied culture on .NET and Fable; allowed invariant forms agree across targets.
8. `Culture.setProcessDefault` sets all four .NET culture properties so third-party formatting follows; Fable is a no-op.
   The ASP.NET adapter passes RequestLocalization's culture to the handler automatically when enabled.
9. `TimeZone.local` returns UTC under `runTest` with `TZ=Pacific/Auckland`; pure `offsetAt`/`toLocal` for `Europe/Berlin`,
   `America/New_York`, `Australia/Lord_Howe` around DST transitions agree on .NET (Linux, Windows) and Node.
10. Guardrails: each row of 4.2 and 7 fires on a positive sample and is silent on its replacement or justified live boundary.
11. An AOT probe loads configuration through a schema and formats with a culture with no IL2xxx/IL3xxx warnings.
12. `Config.watch`: debounced file changes publish valid configs; invalid decodes keep the last good value, log and emit errors;
    environment/args stay static. Use deterministic filesystem events and a test clock.

## 10. Benchmarks (quiet machine only)

| Benchmark | Question | Pass |
|---|---|---|
| `Culture.system` / `TimeZone.local` | cost of explicit machine-default reads | record .NET and Fable results |
| `Format` / `TimeZone.toLocal` | cost of pure formatting and UTC-to-local conversion | record .NET and Fable results |
| `Config.load` of a 30-field schema, 4 sources | is loading negligible at startup? | under 200 µs, no reflection in the profile |

## 11. Order of work

1. **Reified 0.12.0 release**: `Data.merge`/`mergeAll`/`ofEnvironment`, case-insensitive key parse option, nested CLI args and `Secret`.
2. **T31**: Clock/Random/Guid/Log and the test platform move to core `Axial`; `Axial.PlatformService` deleted; Files merges into FileSystem.
3. **Culture and time zone**: sealed values, explicit default-read effects supplied by TestPlatform under `runTest`, pure Format/toLocal,
   Fable Intl backend, process-default startup effect and ASP.NET request-culture adoption. No runtime-context additions.
4. **`Axial.Configuration`**: sources, Reified case option, `Config.load`/`loadData`/plain reads, errors and optional `Config.watch`;
   `HostConfiguration.source` in `Axial.Hosting`; `BrowserApp.queryParameters`; examples for .NET, Node, Generic Host.
5. **Guardrails rules** (4.2, 7) and the repo-wide run at deny.
6. **Docs set** for Configuration and culture/time-zone pages (T33), plus `0.12.0.md` migration rows. Present Axial.Configuration as
   Axial's approach; document Microsoft IConfiguration/Options via Axial.Hosting as a supported alternative, showing the bridge
   through `HostConfiguration.source`, and how hosts can retain Options-based consumers.

## 12. Decisions

DECIDED means owner, 2026-10-31.

| # | Decision | Status |
|---|---|---|
| C1 | Typed model is a Reified `Schema`; Axial's own decoder (old C4) and parser table dropped | DECIDED |
| C2 | Every source produces Reified `Data`; combined with `Data.mergeAll`, later wins | DECIDED |
| C3 | JSON via `Json.tryParseData`, files via `Axial.FileSystem` (`ConfigSource.jsonFile`); own reader (old C5) dropped | DECIDED |
| C4 | Plain string reads `Config.required` / `optional` / `tryFind` | DECIDED |
| C5 | Default load once into a plain value; optional `Config.watch` in 0.12 publishes latest valid config with errors logged/streamed | DECIDED |
| C6 | Capability `IConfig` / `IHasConfig` (member `Config`); fake `Config.ofData` | DECIDED |
| C7 | Explicit `Secret<'v>`, no key-name heuristics, values never rendered | DECIDED |
| C7a | `Secret<'v>` lives in `Reified.Refinements`; errors and ToString are redacted | DECIDED |
| C8 | `Axial.Configuration` in the Axial repo references Reified; core `Axial` does not; Axial 0.12 requires Reified 0.12 | DECIDED |
| C9 | `Axial.PlatformService` deleted in 0.12; env vars become a source; env-var typed reads deleted (old C2) | DECIDED |
| C10 | Sources: env, command line on every host, JSON files, `IConfiguration` adapter, browser query string (old C14) | DECIDED |
| C11 | Scalar parsing is Reified's (old C6 boolean spellings defer to `Schema.bool`) | DECIDED |
| C12 | Case-insensitive keys via a Reified parse option, not Axial canonicalisation | DECIDED |
| C13 | Reified 0.12 `Data.ofCliArgs` handles nested `--Db:Port` | DECIDED |
| C14 | Culture is a plain value outside the runtime context; `Culture.system` is an effect, invariant under `runTest` | DECIDED |
| C15 | Zone and culture are sealed values, not interfaces (old C10) | DECIDED |
| C16 | Guardrails bans the implicit-culture forms of table 4.2 (supersedes old C13) | DECIDED |
| C17 | Guardrails implements F# culture rules itself carrying CA ids; real CA rules enabled for C# | DECIDED |
| C18 | `Culture.setProcessDefault` effect once at startup, not in App; Fable no-op; pure formatting takes a culture | DECIDED |
| C19 | ASP.NET automatically adopts RequestLocalization's request culture as a handler value when `UseRequestLocalization` is used | DECIDED |
| C20 | Time zone is a plain value outside the runtime context; `TimeZone.local` is an effect, UTC under `runTest` | DECIDED |
| C21 | Local-to-UTC deferred; T32 ships UTC-to-local only (old C11) | DECIDED |
| C22 | Drop zone/culture runtime fields and the preservation-only `withClock` fix/test | DECIDED |
