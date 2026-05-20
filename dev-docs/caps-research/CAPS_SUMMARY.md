# FsFlow Capability Approaches: Comparative Summary

Status: research context. The live source of truth is `../PLAN.md`.

This document compares the capability designs explored for FsFlow and adds four approaches that are
meaningfully different from the earlier SRTP / `IServiceProvider` / explicit-interface variants.

See `NEW-APPROACH.md` for the closest research ancestor of the active direction. The implemented public model keeps
`Flow<'env, 'error, 'value>` and carries runtime/system services through ambient runtime state, outside end-user
`'env` signatures.

The important pre-1.0 constraint is simple: compatibility does not matter yet. Pick the shape that should
survive 1.0, then delete or demote everything that points users at a weaker model.

## What FsFlow Is Optimizing For

Capabilities in FsFlow are not just dependency lookup. They should make workflows better in three ways:

- Ergonomics: workflow code should stay close to normal F# and not drown in environment plumbing.
- Compiler safety and fine-grained effects: important logic should expose exactly what it needs, and missing
  dependencies should fail before runtime.
- IDE clarity: signatures, tooltips, errors, and docs should help users instead of showing SRTP noise.

The hard part is that F# 10 improves computation-expression ergonomics, but it does not add row polymorphism,
effect rows, or reusable aliases for SRTP member constraints.

Useful F# 10 facts:

- Computation expressions support custom operations and optimized members such as `BindReturn`,
  `MergeSources`, `ReturnFromFinal`, and `YieldFromFinal`.
- F# 10 improves CE tail positions, typed `let!` annotations, `use! _`, and built-in task `and!`.
- Flexible types such as `#IHasDb` give readable nominal constraints.
- SRTP member constraints remain powerful but poor as a public capability surface.

Primary docs:

- https://learn.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-10
- https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions
- https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types

## Summary Table

| Approach | Primary goal | Ergonomics | Setup burden | User-code burden | Fine-grained | Compile-time safety | IDE clarity | Verdict |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| Boilerplate records + slices | Safe baseline | 2/5 | 5/5 | 4/5 | Yes | Compile-time | Good | Correct but too much wiring |
| `IServiceProvider` edge | .NET host interop | 5/5 | 1/5 | 1/5 | No | Runtime only | Excellent | Keep at edges only |
| Simple Record SRTP | Conservative strict | 3/5 | 3/5 | 2/5 | Partial | Compile-time | Good | Useful fallback, not the 1.0 story |
| Structural Accessors | Structural strict | 4/5 | 2/5 | 2/5 | Yes | Compile-time | Fair | Powerful, but public SRTP cost is too high |
| Structural Accessors + DI bridge | Strict core with DI edge | 4/5 | 3/5 | 2/5 | Yes | Compile-time core | Mixed | Bridge idea is good; SRTP target is not |
| Explicit interface/record hybrid | Nominal strict baseline | 2/5 | 4/5 | 4/5 | Partial | Compile-time | Good | Useful baseline, still too manual |
| Capability Manifest CE | Max ergonomics | 5/5 | 2/5 | 1/5 | Partial | Startup/runtime validation | Excellent | Best authoring feel, weaker static honesty |
| Effect-Indexed Flow | Max fine-grained compiler safety | 2/5 | 5/5 | 4/5 | Yes | Compile-time | Fair | Most honest model, likely too heavy for FsFlow |
| Explicit Local Dependency Records | Max IDE clarity | 4/5 | 2/5 | 2/5 | Yes, manually | Compile-time | Excellent | Best simple/local workflow option |
| Leveled Capability Surface | Best overall | 4/5 | 2/5 | 2/5 | Optional | Compile-time or runtime by level | Good | Recommended direction |

## Existing Research

### Boilerplate Records + Slices

This is ordinary F#: define small records, pass them explicitly, and project them with `TaskFlow.read`.
It is safe and clear, but it is not distinctive enough for FsFlow's capability story. It makes every workflow
author do the wiring that the library should absorb.

Verdict: keep as the fallback mental model.

### `IServiceProvider` Edge

This is the .NET host model:

```fsharp
taskFlow {
    let! db = service<IDb>
    return! db.GetOrder orderId
}
```

It is the best interop story for ASP.NET Core, Aspire, workers, jobs, and hosted services. It is also the
weakest static story: the compiler cannot prove registrations, and the workflow type does not reveal its
dependencies.

Verdict: keep for edges, examples, and bridges. Do not make it the strict core model.

### Simple Record SRTP

This groups capabilities into records and uses SRTP accessors for those records. It reduces boilerplate, but
the grouping pressure is real. Broad records such as `SystemCaps` and `RuntimeCaps` hide the fine-grained effects
FsFlow should preserve.

Verdict: useful fallback for advanced F# users, not the main story.

### Structural Accessors

This was the strongest earlier strict design:

```fsharp
module Cap =
    let inline db (env: ^env) : IDb =
        (^env : (member Db : IDb) env)
```

It gives anonymous-record provisioning, automatic composition, and fine-grained compile-time checks.
The failure mode is product feel:

- no reusable SRTP requirement aliases
- noisy inferred signatures
- F#-only surface
- convention-based member names instead of named contracts

Verdict: valuable research, but too much SRTP leaks into user experience for a 1.0 default.

### Structural Accessors + DI Bridge

The bridge idea survives. Strict workflows should be runnable from a normal .NET host without leaking
`IServiceProvider` through core logic.

The bridge target should change: bridge into an explicit nominal capability surface, not into public SRTP
requirements.

Verdict: keep bridge thinking, change the strict target.

### Explicit Interface/Record Hybrid

This uses ordinary interfaces and records to model capabilities. It is readable and compiler-safe, but the
earlier form still required too much hand-written environment and aggregate-interface boilerplate.

Verdict: useful baseline. The better version needs a simpler public convention and clearer separation between
runtime capabilities, app capabilities, helper modules, and edge bridges.

## New Approach 1: Capability Manifest CE

Primary goal: maximize ergonomics.

This approach treats capability usage as a manifest collected by the builder. The workflow body gets very clean
syntax, and the manifest can be inspected, validated at startup, logged, or used to build docs.

Shape:

```fsharp
let poll deviceId =
    taskFlow {
        requires {
            runtime Log
            runtime Clock
            service IDeviceClient
        }

        withRetry networkRetry
        measure "device.poll"
        annotate "deviceId" deviceId

        do! Log.info "Polling"
        let! client = Service.get<IDeviceClient>
        return! client.Read deviceId
    }
```

Conceptual type:

```fsharp
TaskFlow<'env, AppError, DeviceStatus>
```

With attached metadata:

```fsharp
{ Runtime = [ Log; Clock ]
  Services = [ typeof<IDeviceClient> ]
  Policies = [ Retry; Measure "device.poll" ] }
```

Mechanics:

- `taskFlow` becomes a plan/spec builder, not just a direct flow builder.
- `requires { ... }` is a custom-operation sub-DSL that records declared capabilities.
- `Run` validates declarations against a runtime registry or service provider.
- Runtime policies such as retry, timeout, measure, and annotate become block decorators.

Pros:

- best authoring experience
- excellent IDE experience because workflow signatures stay simple
- startup validation can catch many missing services early
- capability metadata is useful for diagnostics, generated docs, and observability
- aligns with the older `RUNTIME_CAPS.md` policy-based CE intent

Cons:

- capability honesty is metadata, not type-level proof
- users can forget to declare a capability unless accessors enforce registration
- more builder machinery
- stricter semantics require a plan/spec representation

Verdict:

This is the best ergonomics-max design. It is not the strict core answer because it moves safety from the compiler
to validation.

## New Approach 2: Effect-Indexed Flow

Primary goal: maximize compiler safety and fine-grained effects.

This approach adds a phantom capability index to the workflow type. Requirements become part of the type, not just
the environment constraint.

Shape:

```fsharp
type NoCaps = NoCaps
type Requires<'cap, 'rest> = Requires

type LogCap = LogCap
type ClockCap = ClockCap
type DeviceClientCap = DeviceClientCap

type TaskFlow<'caps, 'env, 'error, 'value>
```

Capability operations carry exact requirement indexes:

```fsharp
module Log =
    val info :
      string ->
        TaskFlow<Requires<LogCap, NoCaps>, 'env, 'error, unit>

module Device =
    val client :
        TaskFlow<Requires<DeviceClientCap, NoCaps>, 'env, 'error, IDeviceClient>
```

Composition needs a type-level union:

```fsharp
taskFlow {
    do! Log.info "Polling"
    let! client = Device.client
    return! client.Read deviceId
}
```

Conceptual result:

```fsharp
TaskFlow<Requires<LogCap, Requires<DeviceClientCap, NoCaps>>, 'env, AppError, DeviceStatus>
```

To run:

```fsharp
TaskFlow.run
    (capabilities {
        provide LogCap logWriter
        provide DeviceClientCap deviceClient
    })
    (poll deviceId)
```

Pros:

- finest-grained static effect tracking
- workflow type explicitly lists effects
- no ambient dependency access by accident
- can support tooling that shows effect sets directly

Cons:

- F# cannot naturally compute and normalize type-level set union
- duplicates, ordering, and subset checks become painful
- users see phantom type machinery in signatures
- likely requires many overloads or plugin-like machinery to be usable
- probably fights F# instead of flowing with it

Verdict:

This is the most honest design on paper. It is also too heavy for FsFlow's pragmatic goals unless FsFlow is willing
to become a type-level programming library.

## New Approach 3: Explicit Local Dependency Records

Primary goal: maximize IDE clarity.

This approach does not try to infer a reusable capability surface. A workflow declares one local dependency record
and uses it directly.

Shape:

```fsharp
type SubmitOrderDeps =
    { Log : LogEntry -> unit
      Orders : IOrderRepository
      Email : IEmailSender }

let submitOrder command : TaskFlow<SubmitOrderDeps, AppError, Guid> =
    taskFlow {
        let! deps = TaskFlow.env

        deps.Log
            { Level = LogLevel.Information
              Message = "Submitting order"
              TimestampUtc = DateTimeOffset.UtcNow }

        let! order = deps.Orders.Save command
        do! deps.Email.SendConfirmation order

        return order.Id
    }
```

Pros:

- best tooltip and error-message clarity
- no SRTP
- no helper convention required
- no generator or extra tooling
- excellent for tests, examples, and small features

Cons:

- no automatic accumulation across independently authored helpers
- shared operations require projection, wrapper functions, or duplicated fields
- feature records can become coarse if overused
- runtime/app split is not visible unless the user models it manually

Verdict:

This is the IDE-max path and should remain a first-class option. It is not the best core capability model because
composition across modules stays manual.

## New Approach 4: Leveled Capability Surface

Primary goal: optimize across ergonomics, compiler safety, fine-grained effects, IDE clarity, and host interop.

This stops treating one capability mechanism as mandatory. It offers levels:

1. explicit dependency records for small/local workflows
2. `RuntimeContext<'runtime, 'env>` when runtime/app separation matters
3. `IServiceProvider`-backed runtime/app for DI-heavy hosts
4. nominal `IHasX` helpers for reusable strict effects

Runtime split shape:

```fsharp
type RuntimeContext<'runtime, 'env> =
    { Runtime : 'runtime
      Environment : 'env
      CancellationToken : CancellationToken }
```

Provider-backed shape:

```fsharp
type AppRuntime =
    { Log : LogEntry -> unit
      Clock : IClock
      Services : IServiceProvider }

type AppContext =
    RuntimeContext<AppRuntime, IServiceProvider>
```

Nominal helper shape when strict reuse matters:

type IHasOrders =
    abstract Orders : IOrderRepository

type IHasEmail =
    abstract Email : IEmailSender

type OrderApp =
    { OrdersValue : IOrderRepository
      EmailValue : IEmailSender }

    interface IHasOrders with
        member x.Orders = x.OrdersValue

    interface IHasEmail with
        member x.Email = x.EmailValue
```

Strict access helpers:

```fsharp
module Orders =
    val repository :
      unit ->
        TaskFlow<RuntimeContext<'runtime, #IHasOrders>, 'error, IOrderRepository>
```

Provider-backed workflow:

```fsharp
let submitOrder command =
    taskFlow {
        let! orders = Service.get<IOrderRepository>
        let! email = Service.get<IEmailSender>
        ...
    }
```

Optional ergonomic edge DSL:

```fsharp
taskFlow {
    withRetry networkRetry
    measure "orders.submit"

    do! Log.info "Submitting order"
    return! OrdersWorkflow.submit command
}
```

Pros:

- users can choose the right ceremony level
- explicit records stay the simplest default
- DI-heavy apps get a real provider-backed path
- nominal helpers remain available for compile-time strict reuse
- no generator, plugin, or build-time magic
- preserves `RuntimeContext<'runtime,'env>` and the existing runtime/env split

Cons:

- docs must make the levels clear
- provider-backed flows trade static service lists for runtime/startup validation
- nominal helpers still have ceremony when used

Verdict:

This is the best overall 1.0 direction.

## Recommendation For FsFlow

Because FsFlow is pre-1.0, make the breaking move now.

For the compressed recommended model, see `CAPS_RECOMMENDED_MODEL.md`.
For a new-user walkthrough, see `CAPS_RECOMMENDED_WALKTHROUGH.md`.
Both include LLM-coding guidance: explicit records are the most agent-friendly default, followed by named
`RuntimeContext` records, provider-backed host code, and nominal helpers only for established reusable effects.

Recommended direction:

1. Do not make one capability style mandatory.
2. Keep explicit dependency records as the default small/local workflow option.
3. Keep `RuntimeContext<'runtime, 'env>` as the execution carrier when runtime/app separation matters.
4. Treat `IServiceProvider` as a valid provider-backed app/runtime shape for DI-heavy hosts, not only as an
   awkward edge escape hatch.
5. Add hosting adapters from `ILogger`, clocks, metrics, tracing, and `IServiceProvider` into FsFlow runtime
   contracts.
6. Use hand-written nominal interfaces only for reusable strict capabilities where static requirements pay for the
   boilerplate.
7. Keep cancellation token as executor mechanics, not as a user capability.
8. Keep logging generic (`LogEntry -> unit` or an FsFlow-owned abstraction), with `ILogger` only as an adapter.
9. Demote SRTP structural accessors to advanced research, not the default strict API.
10. Only add plan/spec custom operations such as `withRetry`, `measure`, and `annotate` if FsFlow commits to a
    policy-aware builder model.

## Concrete 1.0 Shape

Ship these layers:

```text
FsFlow.Core
  TaskFlow, RuntimeContext, read/readRuntime/readEnvironment, cancellation mechanics

FsFlow.Capabilities
  optional nominal runtime/application helper conventions for reusable strict capabilities

FsFlow.Hosting
  IServiceProvider-backed runtime/app adapters, ASP.NET/Aspire edge helpers, startup validation
```

Default guidance:

```text
Use explicit dependency records first.
Use RuntimeContext when runtime/app separation matters.
Use IServiceProvider-backed runtime/app when host ergonomics matter.
Use nominal capabilities only for reusable strict helpers.
Use SRTP structural accessors only for advanced F# experiments.
```

Final position:

> Best 1.0 shape: levels, not a mandatory capability framework. Records for clarity,
> provider-backed runtime for .NET ergonomics, nominal helpers for reusable strict effects.
