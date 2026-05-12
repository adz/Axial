---
weight: 20
title: Environment Slicing
description: Deep dive into the Record Pattern for dependency management.
---

# Environment Slicing (The Record Pattern)

The Record Pattern is the most common way to manage dependencies in FsFlow. It uses standard F# records to define the "world" a workflow lives in.

## Projecting from the Environment

When a workflow needs a dependency, it "reads" it from the environment record.

```fsharp
type AppEnv = 
    { Gateway: IPingGateway
      Logger: ILogger }

let ping =
    flow {
        // 'env' is the full AppEnv record
        let! gateway = Flow.read (fun env -> env.Gateway)
        return! gateway.Ping()
    }
```

### The Shorthand: `_.Field`

F# provides a nice shorthand for these simple projections. Instead of `(fun env -> env.Gateway)`, you can write `_.Gateway`. This makes the code much cleaner and easier to read.

```fsharp
let ping =
    flow {
        let! gateway = Flow.read _.Gateway
        let! logger = Flow.read _.Logger
        
        logger.Info "Starting ping"
        return! gateway.Ping()
    }
```

## Slicing with `localEnv`

"Slicing" is the process of taking a large environment and projecting it down to a smaller one required by a sub-flow. This keeps your workflows "honest"—they only see the dependencies they actually use.

```fsharp
type SmallEnv = { Logger: ILogger }

let smallWorkflow : Flow<SmallEnv, unit, unit> = ...

let bigWorkflow : Flow<AppEnv, unit, unit> =
    smallWorkflow
    |> Flow.localEnv (fun env -> { Logger = env.Logger })
```

## Splitting Runtime Services from App Dependencies

In complex apps, you often want to separate **Operational Services** (logging, metrics, cancellation) from **Application Services** (gateways, repositories). FsFlow provides `RuntimeContext<'runtime, 'env>` for this.

```fsharp
type RuntimeServices = { Log: string -> unit }
type AppEnv = { Gateway: IPingGateway }

let workflow : Flow<RuntimeContext<RuntimeServices, AppEnv>, unit, unit> =
    flow {
        // Read from the 'runtime' half
        let! log = Flow.readRuntime _.Log
        // Read from the 'env' half
        let! gateway = Flow.readEnvironment _.Gateway

        log "starting"
        return! gateway.Ping()
    }
```

## The Capability Module

The `Capability` module provides helpers that work across all flow types (`Flow`, `Flow`, `Flow`) using a single API.

- `Capability.service`: Polymorphic version of `read`.
- `Capability.runtime`: Polymorphic version of `readRuntime`.
- `Capability.environment`: Polymorphic version of `readEnvironment`.

```fsharp
let log message =
    flow {
        let! logger = Capability.service _.Logger
        logger.Log message
    }
```

## Layering and Composition

Layers are flows that produce a derived environment. Use `Flow.provideLayer` to "connect" a layer to a downstream workflow.

```fsharp
let appLayer : Flow<RuntimeServices, AppError, AppDependencies> = ...
let workflow : Flow<AppDependencies, AppError, Response> = ...

let runnable = workflow |> Flow.provideLayer appLayer
```

The downstream workflow stays typed against the smaller environment, while the final runnable workflow accepts the outer environment needed to build it.

---

## Next Steps

If you need to decouple your workflows from specific record types entirely (e.g., for a shared library), read about the [CAPS Pattern](./capabilities/).
