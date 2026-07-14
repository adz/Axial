---
title: Node Hosting
linkTitle: Node
weight: 20
description: Running Fable Flow applications under Node signals, process exit, arguments, and process.env.
type: docs
---


`Axial.Flow.Hosting.Node` is a JavaScript-only Fable package for Node processes.

```sh
dotnet add package Axial.Flow.Hosting.Node
```

The NuGet package uses a .NET target asset because Fable consumes F# projects through MSBuild. It is not a .NET
runtime implementation: calling its entry points without Fable JavaScript fails immediately. Calling it in a browser
also fails because the Node `process` object is required.

## Root Application

Keep Node mechanics outside the application workflow:

```fsharp
type StartupInputs =
    { Arguments: string list
      EnvironmentVariables: IEnvironmentVariables }

let inputs =
    { Arguments = NodeApp.arguments()
      EnvironmentVariables = NodeEnvironment.live }

let application : Flow<StartupInputs, AppError, unit> =
    program
    |> Flow.provide Live.appLayer
```

`NodeEnvironment.live` implements `IEnvironmentVariables` over `process.env`. `TryGet`, `Set`, and `GetAll` use the
live Node object. `Expand` expands `$NAME` and `${NAME}` forms.

## Run and Exit

At the Fable entry point:

```fsharp
open Axial.Flow.Hosting.Node

NodeApp.run AppError.describe inputs application
|> Async.StartImmediate
```

`NodeApp.run`:

- subscribes to `SIGINT` and `SIGTERM`;
- requests coordinated `App.Stop()` on either signal;
- removes both handlers after completion;
- writes typed failures and defects through `console.error`;
- sets `process.exitCode` only after the root scope closes.

It does not call `process.exit()`, because doing so would terminate the event loop before asynchronous finalizers can
finish.

The exit-code mapping is success `0`, typed failure `1`, defect `2`, SIGINT `130`, and SIGTERM `143`. An interruption
not caused by an installed signal uses `130`. A cleanup defect takes precedence over the interruption code.

Use `NodeApp.start` when another Node module needs the `AppHandle`:

```fsharp
let running = NodeApp.start AppError.describe inputs application

async {
    let! exit = running.Completion
    reportCompletion exit
}
|> Async.StartImmediate
```

## Logging and Telemetry

Supply Node logging as an explicit `ILog` implementation in the application environment. For tracing, install
`Axial.Flow.Telemetry.JavaScript` with the host's `@opentelemetry/api` object before starting the application. Hosting
does not choose an SDK, exporter, or context manager.

## Build Shape

A minimal project uses Fable and an ES module package:

```json
{
  "type": "module",
  "scripts": {
    "build": "dotnet fable --outDir dist",
    "start": "node dist/Program.fs.js"
  }
}
```

The application and layers remain normal F# source. Only `Program.fs` needs Node hosting calls.
