---
title: Environment Variables
description: Typed configuration reads with a failure channel instead of nulls.
---

Configuration read from the environment is the classic source of a late, confusing startup failure: a missing
variable surfaces as a `null`, and a malformed one as a parse exception somewhere further in. `Axial.PlatformService`
splits this into two modules — one for raw access, one for typed reads with a failure channel.

```fsharp
open System
open Axial
open Axial.PlatformService
```

## Raw access

`EnvironmentVariables` returns what is there, with no opinion about what is required:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
EnvironmentVariables.tryGet name    // string option
EnvironmentVariables.getAll         // IReadOnlyDictionary<string, string>
EnvironmentVariables.set name value
EnvironmentVariables.clear name
EnvironmentVariables.expand text    // expands %VAR% references
```

These never fail — an absent variable is `None`.

## Typed reads

`EnvironmentVariable` reads a variable, requires it, and parses it, failing with `EnvironmentVariableError`:

```fsharp
let readPort : Flow<BaseRuntime, EnvironmentVariableError, int> =
    EnvironmentVariable.getInt "PORT"
```

The environment is named concretely here rather than written as `#IHas<IEnvironmentVariables>`. A flow bound to a
plain value, with no parameters, hits F#'s value restriction if its environment stays generic. Give the value a
concrete environment type, or make it a function of something.

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

`EnvironmentVariableError` has two cases, and they carry enough to write a useful message:

- `MissingVariable name` — the variable was not set.
- `InvalidVariable (name, value, expected)` — it was set but did not parse, with what was expected.

`EnvironmentVariableErrors.describe` formats either into a sentence such as `Environment variable 'PORT' had value
'eighty' but expected an integer.`

## Failing startup once, with everything wrong listed

Because the reads are flows with a typed error, configuration validation composes into one workflow that either
produces the settings record or reports what is wrong:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let readSettings =
    flow {
        let! port = EnvironmentVariable.getInt "PORT"
        let! endpoint = EnvironmentVariable.getUri "API_ENDPOINT"
        let! timeout = EnvironmentVariable.getTimeSpan "API_TIMEOUT"
        return { Port = port; Endpoint = endpoint; Timeout = timeout }
    }
```

This binds sequentially, so it stops at the first problem. Run it as a layer during startup and the application
cannot reach its first request with unparsed configuration — see
[layers](/dependencies/layers.html) for provisioning failure.

## Supplying the service

`EnvironmentVariables.live` reads the current process environment. `EnvironmentVariables.fromPairs` builds a fixed
provider, which is how tests avoid mutating global process state:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let environment =
    EnvironmentVariables.fromPairs
        [ "PORT", "8080"
          "API_ENDPOINT", "https://api.example.com" ]
```

Prefer `fromPairs` over setting real variables in a test. Process environment state is shared across a test run, so
mutating it makes tests order-dependent.
