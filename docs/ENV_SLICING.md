---
weight: 40
title: Environment Slicing
---

# Environment Slicing

This page shows two ways to keep an FsFlow workflow honest about dependencies:
small record environments with `localEnv`, and interface-based capability environments.
For task-oriented work, the same idea can split into runtime services and application capabilities
with `RuntimeContext<'runtime, 'env>`.

The common goal is the same in both styles: each flow should depend on the smallest environment
it actually needs.

## Start With A Small Environment Record

For most code in this repo, this is the best default:

```fsharp
type FetchResponseEnv =
    { Gateway: IPingGateway
      AttemptCount: int ref
      Log: string -> unit }

let fetchResponse (plan: RequestPlan) : TaskFlow<FetchResponseEnv, AppError, Response> =
    taskFlow {
        let! gateway = TaskFlow.read _.Gateway
        let! attempts = TaskFlow.read _.AttemptCount
        let! log = TaskFlow.read _.Log

        log (sprintf "gateway call attempt=%d url=%s" (attempts.Value + 1) plan.Url)

        let! response = gateway.Ping plan
        return response
    }
```

This keeps the flow signature honest without forcing it to depend on the whole application
environment.

## Project From A Larger Application Environment

When the real application environment is larger, project it down:

```fsharp
type AppEnv =
    { Gateway: IPingGateway
      AuditStore: IAuditStore
      AttemptCount: int ref
      Log: string -> unit }

let fetchResponseInAppEnv plan : TaskFlow<AppEnv, AppError, Response> =
    fetchResponse plan
    |> TaskFlow.localEnv (fun env ->
        { Gateway = env.Gateway
          AttemptCount = env.AttemptCount
          Log = env.Log })
```

This is the simplest way to compose bigger programs from smaller flows.

## Split Runtime Services From Application Capabilities

When a task boundary needs both operational services and application dependencies, use
`RuntimeContext<'runtime, 'env>` rather than forcing everything into one record:

```fsharp
type RuntimeServices =
    { Log: string -> unit }

type AppEnv =
    { Gateway: IPingGateway
      AttemptCount: int ref }

let fetchResponse : TaskFlow<RuntimeContext<RuntimeServices, AppEnv>, AppError, Response> =
    taskFlow {
        let! log = TaskFlow.readRuntime _.Log
        let! gateway = TaskFlow.read _.Gateway

        log "starting request"
        return! gateway.Ping()
    }
```

Use this shape when operational concerns and app dependencies deserve different lifetimes or ownership.

## Named Cap Sets (CAPS)

When the boundary itself should name the capability contract, use CAPS instead of ad hoc
interface constraints.

```fsharp
open System
open FsFlow

type IClock =
    abstract UtcNow: unit -> DateTimeOffset

type ILogger =
    abstract Log: string -> unit

type LoginCaps =
    inherit Needs<IClock>
    inherit Needs<ILogger>
    abstract Clock : IClock
    abstract Logger : ILogger

type LoginRuntime =
    { ClockService: IClock
      LoggerService: ILogger }

    interface LoginCaps with
        member x.Clock = x.ClockService
        member x.Logger = x.LoggerService

    interface Needs<IClock> with
        member x.Dep = x.ClockService

    interface Needs<ILogger> with
        member x.Dep = x.LoggerService

let login : TaskFlow<#LoginCaps, AppError, unit> =
    taskFlow {
        let! clock = Env<IClock>
        let! now = Env<IClock> _.UtcNow
        do! Env<ILogger> (fun log -> log.Log $"Starting login at {now}")
        return ()
    }
```

This style works when:

- several application environments should satisfy the same capability contract
- you want public boundaries to accept larger runtimes without exact type matches
- the workflow should read like a named use case, not a raw environment shim

For record-shaped projections or a small local seam, `localEnv` is still the simplest option.

## Where Record Slicing Helps

Record-based slicing is useful when:

- you want straightforward code and predictable compiler errors
- most flows live inside one application and only need projection from a larger env
- `localEnv` already gives you the composition step cleanly

## Capabilities and Service Discovery

For complex applications, FsFlow provides a structured way to manage record projections and
service-provider interop through the `Capability` module. Keep this as an edge helper; use
named cap sets for the primary public dependency boundary.

```fsharp
type ILogger = abstract Log : string -> unit

let log message =
    taskFlow {
        let! logger = Capability.service _.Logger
        return logger.Log message
    }
```

`Capability.service` reads a value from a record projection, so the workflow only depends
on the field it actually needs.

## Layering and Composition

Layers are flows that derive the smaller environment required by a downstream flow. Use
`Flow.provideLayer`, `AsyncFlow.provideLayer`, or `TaskFlow.provideLayer` when the layer itself
can validate, fail, or read from an outer environment.

```fsharp
type RuntimeServices =
    { ConnectionString : string }

type AppDependencies =
    { Database : IDatabase }

let appLayer : TaskFlow<RuntimeServices, AppError, AppDependencies> =
    taskFlow {
        let! connectionString = TaskFlow.read _.ConnectionString
        let! database = Database.connect connectionString
        return { Database = database }
    }

let workflow : TaskFlow<AppDependencies, AppError, Response> =
    taskFlow {
        let! database = TaskFlow.read _.Database
        return! database.Load()
    }

let runnable : TaskFlow<RuntimeServices, AppError, Response> =
    workflow
    |> TaskFlow.provideLayer appLayer
```

The downstream workflow stays typed against the smaller environment, while the runnable workflow
accepts the outer environment needed to build it.

## Runtime Context vs. Application Environment

In TaskFlow, we distinguish between:

1.  **Runtime Context (`'runtime`)**: Low-level operational services like logging, 
    cancellation, and retry policies. These are usually provided by the infrastructure.
2.  **Application Environment (`'env`)**: High-level domain services like repositories,
    gateways, and business logic dependencies.

The `RuntimeContext<'runtime, 'env>` type carries both, allowing you to use 
`TaskFlow.readRuntime` and `TaskFlow.read` to access the appropriate half.

## Why Layering?

- **Testability**: you can run the workflow with a small test environment or replace the layer.
- **Modularity**: components can define their own environment requirements independently.
- **Explicit dependencies**: the layer type says what it needs to start and what it provides to the workflow.

## Next

Read [`docs/GETTING_STARTED.md`](./GETTING_STARTED.md) for the main workflow model, then
[`docs/TASK_ASYNC_INTEROP.md`](./TASK_ASYNC_INTEROP.md) for task and async boundaries, then
[`docs/TROUBLESHOOTING_TYPES.md`](./TROUBLESHOOTING_TYPES.md) if you start pushing the type
system harder.
