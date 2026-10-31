---
title: Configuration and environment variables
description: Layered typed configuration decoded by a Reified schema, with accumulated keyed errors.
---

`Axial.Configuration` replaces ad-hoc environment reads with layered configuration. Every source produces
`Reified.Data`; later sources win, and one Reified schema decodes the merged result. Missing and malformed settings
are values in a `ConfigError` list, not exceptions, and all of them are reported at once.

> **Fable:** with Reified 0.12.0, source loading, merging, `Config.loadData`, `Config.tryFind` and `Config.watch`
> plumbing work on Fable. Typed `schema<...>` construction and `Config.load` decoding require Reified 0.12.1 until
> the upstream Fable schema-builder fix lands.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Configuration
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript
open Reified
open Reified.SchemaDSL

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open System
open Axial
open Axial.Configuration
open Reified
```

## Sources

| Source | Constructor | Notes |
| --- | --- | --- |
| Environment | `ConfigSource.environment (Some "MYAPP")` | Reads the process environment once, splitting `MYAPP__Db__Port` on `__` |
| Fixed environment | `ConfigSource.environmentOf prefix pairs` | The same conversion from pairs supplied by a test or host |
| Command line | `ConfigSource.commandLine argv` | `--Db:Port=5432`, `--flag`, `--no-flag`; positionals arrive under `_` |
| JSON file | `ConfigSource.jsonFile path` | Reads through `IHasFileSystem`; `optionalJsonFile` treats a missing file as an empty object |
| JSON text | `ConfigSource.json name text` | For embedded or already-read JSON |
| Browser query | `ConfigSource.queryString search` | `?Db.Port=5432`; use `BrowserApp.queryParameters ()` from the browser host |
| Microsoft configuration | `HostConfiguration.source "host" configuration` | In `Axial.Hosting`; keeps `appsettings.json`, user secrets and `CreateDefaultBuilder(args)` |
| Fixed data | `ConfigSource.ofData name data` | Defaults and fixtures |

List order is precedence, and later wins. Keys match case-insensitively across layers, so `Db.Port` from JSON,
`MYAPP__DB__PORT` from the environment and `--Db:Port` from arguments address the same field.

## Decode a typed model

```fsharp
type Db = { Host: string; Port: int }

type Settings = { Db: Db; Timeout: int }

let settingsSchema () =
    schema<Settings> {
        field (fun (settings: Settings) -> settings.Db) {
            withSchema (
                schema<Db> {
                    field (fun (db: Db) -> db.Host)
                    field (fun (db: Db) -> db.Port)
                    construct (fun host port -> { Host = host; Port = port })
                }
            )
        }
        field (fun (settings: Settings) -> settings.Timeout)
        construct (fun db timeout -> { Db = db; Timeout = timeout })
    }

let loadSettings argv =
    Config.load
        (settingsSchema ())
        [ ConfigSource.ofData "defaults" (Data.Object [ "Db", Data.Object [ "Host", Data.Text "localhost" ] ])
          ConfigSource.environment (Some "MYAPP")
          ConfigSource.commandLine argv ]
```

```fsharp run
loadSettings [ "--Db:Port=5432"; "--Timeout=30" ]
|> Flow.run ()
|> shouldEqual
    (Exit.Success
        { Db = { Host = "localhost"; Port = 5432 }
          Timeout = 30 })
```

`ConfigError.Invalid` carries every `SchemaIssue` keyed by `SchemaPath`. `ConfigError.describe` renders them in
invariant, path-sorted order and lists the spelling each source uses for an omitted path:

```fsharp
let describeFailure config errors =
    errors |> List.map (ConfigError.describe config) |> String.concat Environment.NewLine
```

A source failure (unreadable file or malformed JSON) is reported as `ConfigError.SourceFailed`, and schema decoding
is skipped so one bad file does not produce misleading omission messages.

## Secrets

Declare a secret in the model with `Secret<'v>` from `Reified.Refinements`. Its value never appears in `ToString`,
`%A`, `%O`, schema errors, `ConfigError.describe` or log entries:

```fsharp
open Reified.Refinements

type Credentials = { Password: Secret<string> }

let credentialsSchema () =
    schema<Credentials> {
        field (fun (credentials: Credentials) -> credentials.Password) {
            withSchema (Schema.secretWith Schema.text)
        }
        construct (fun password -> { Password = password })
    }
```

Use `Config.processArg` to pass a secret to a process. It yields a redacted argument, so plans, transcripts and
failures show `***` while the child receives the real value:

```fsharp no-check reason="Process examples live in the Process documentation set"
let deploy (token: Secret<string>) =
    Axial.Process.DSL.cmd $"deploy --token {Config.processArg token}"
```

## Plain string reads

For one or two settings a schema is ceremony. `Config.required` and `Config.optional` read text from an
`IHasConfig` environment, and `Config.tryFind` is the pure form:

```fsharp
type ToolEnv =
    { Config: IConfig }

    interface IHasConfig with
        member this.Config = this.Config

let readEndpoint : Flow<ToolEnv, ConfigError, string> = Config.required "Api.Endpoint"
let readProxy : Flow<ToolEnv, ConfigError, string option> = Config.optional "Api.Proxy"
```

Numbers and booleans come back as their text; use a schema when the value needs typed validation.

## Watch file sources

`Config.watch sources schema` loads once and returns a `SubscriptionRef` of the latest successfully decoded value.
File changes are debounced, all sources are re-read and merged, and a failed decode keeps the last good value, logs
the failure, and emits it on the `Errors` stream. Environment and command-line sources remain static.

```fsharp
let watchSettings<'env when 'env :> IHasFileSystem> argv =
    Config.watch
        [ ConfigSource.jsonFile "appsettings.json"
          ConfigSource.commandLine argv ]
        (settingsSchema ())
```

## Load once, pass the value

Configuration is usually loaded once at startup into a plain record, like Elm flags. Put the value, or the section
a service needs, in the application environment and read it with `Flow.envWith`; build handlers and Elmish programs
as closures over it. `Config.section path schema` exists for libraries that own one section.

The Microsoft `IConfiguration` path is a supported alternative. Pass
`HostConfiguration.source "host" builder.Configuration` as one layer, or keep `IOptions<T>` for components that
already depend on it.
