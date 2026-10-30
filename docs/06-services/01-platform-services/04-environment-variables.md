---
title: Environment Variables
description: Typed configuration reads with a failure channel instead of nulls.
---

A missing environment variable can surface as `null`, and a malformed value can throw a parse exception later. `Axial.PlatformService`
splits this into two modules: one for raw access, and one for typed reads with a failure channel. Unlike runtime
clock/random/GUID values, environment-variable authority stays explicit through `IHasEnvironmentVariables` in `'env`.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open System
open Axial
open Axial.PlatformService
```

## Raw access

`EnvironmentVariables` provides raw access without requiring values:

| Function | Result |
| --- | --- |
| `EnvironmentVariables.tryGet name` | `string option` |
| `EnvironmentVariables.getAll` | `IReadOnlyDictionary<string, string>` |
| `EnvironmentVariables.set name value` | Sets a variable |
| `EnvironmentVariables.clear name` | Removes a variable |
| `EnvironmentVariables.expand text` | Expands `%VAR%` references |

`EnvironmentVariables.fromPairs` builds an in-memory set for tests:

```fsharp transcript
> open Axial;;
> open Axial.PlatformService;;
> let runtime () = let variables = EnvironmentVariables.fromPairs [ "REGION", "eu" ] in { new IHasEnvironmentVariables with member _.EnvironmentVariables = variables };;
> (EnvironmentVariables.tryGet "REGION" : Flow<IHasEnvironmentVariables, Never, string option>) |> Flow.run (runtime ());;
val it: Exit<string option,Never> = Success (Some "eu")

> (EnvironmentVariables.tryGet "MISSING" : Flow<IHasEnvironmentVariables, Never, string option>) |> Flow.run (runtime ());;
val it: Exit<string option,Never> = Success None
```

These never fail; an absent variable is `None`.

## Typed reads

`EnvironmentVariable` reads a variable, requires it, and parses it, failing with `EnvironmentVariableError`:

```fsharp
type ConfigurationEnv =
    { Variables: IEnvironmentVariables }
    interface IHasEnvironmentVariables with member this.EnvironmentVariables = this.Variables

let readPort : Flow<ConfigurationEnv, EnvironmentVariableError, int> =
    EnvironmentVariable.getInt "PORT"
```

The environment is named concretely here rather than written as `#IHasEnvironmentVariables`, because a flow bound
to a plain value hits F#'s value restriction while its environment is still generic: a value cannot be generalised
over a type variable.

Adding a parameter removes the restriction, because a function can be. A `unit` parameter is enough, and the result
stays usable in any environment supplying the service:

```fsharp
let readPortIn () : Flow<#IHasEnvironmentVariables, EnvironmentVariableError, int> =
    EnvironmentVariable.getInt "PORT"
```

Prefer naming the environment when a workflow belongs to one application, and the parameterised form when a helper
is genuinely shared across environment shapes.

| Function | Result |
| --- | --- |
| `get` | `string`, failing when absent |
| `tryGet` | `string option`, never failing |
| `getInt`, `getInt64` | Integers |
| `getDouble`, `getDecimal` | Numbers |
| `getBool` | Booleans |
| `getGuid` | GUIDs |
| `getUri` | URIs |
| `getTimeSpan` | Durations |

Numeric parsing uses the invariant culture, so a variable set on a machine with a comma decimal separator reads the
same everywhere.

`EnvironmentVariableError` has two cases:

- `MissingVariable name`: the variable was not set.
- `InvalidVariable (name, value, expected)`: it was set but did not parse, with what was expected.

`EnvironmentVariableErrors.describe` formats either into a sentence such as `Environment variable 'PORT' had value
'eighty' but expected an integer.`

## Validating configuration at startup

Typed reads compose into a workflow that produces settings or fails with `EnvironmentVariableError`:

```fsharp
type Settings = { Port: int; Endpoint: Uri; Timeout: TimeSpan }

let readSettings : Flow<ConfigurationEnv, EnvironmentVariableError, Settings> =
    flow {
        let! port = EnvironmentVariable.getInt "PORT"
        let! endpoint = EnvironmentVariable.getUri "API_ENDPOINT"
        let! timeout = EnvironmentVariable.getTimeSpan "API_TIMEOUT"
        return { Port = port; Endpoint = endpoint; Timeout = timeout }
    }

let withVariables pairs =
    { Variables = EnvironmentVariables.fromPairs pairs }
```

```fsharp run
readSettings
|> Flow.run (withVariables [ "PORT", "8080"; "API_ENDPOINT", "https://api.example.com"; "API_TIMEOUT", "00:00:30" ])
|> shouldEqual (Exit.Success { Port = 8080; Endpoint = Uri "https://api.example.com"; Timeout = TimeSpan.FromSeconds 30.0 })

readSettings
|> Flow.run (withVariables [ "PORT", "eighty" ])
|> shouldEqual (Exit.Failure(Cause.Fail(EnvironmentVariableError.InvalidVariable("PORT", "eighty", "an integer"))))
```

This binds sequentially, so it stops at the first problem. Run it as a layer during startup and the application
cannot reach its first request with unparsed configuration; see
[layers](/layers/index.html) for provisioning failure.

## Supplying the service

`EnvironmentVariables.live` reads the current process environment. `EnvironmentVariables.fromPairs` builds a fixed
provider, which is how tests avoid mutating global process state:

```fsharp
let environment =
    EnvironmentVariables.fromPairs
        [ "PORT", "8080"
          "API_ENDPOINT", "https://api.example.com" ]
```

Prefer `fromPairs` over setting real variables in a test. Process environment state is shared across a test run, so
mutating it makes tests order-dependent.
