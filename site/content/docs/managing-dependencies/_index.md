---
weight: 40
title: Managing Dependencies
description: How FsFlow models dependency boundaries after the Registry / Scope / Layer foundation.
type: docs
---


In FsFlow, `env` can be any type you want. Within a `flow {}`, `Flow.env` gives you the whole
environment value, and `Flow.read` projects the piece you need from it. That is the basic shape of
dependency handling in FsFlow: keep the environment explicit, and read only what the flow actually
needs.

> **Tutorials Available:** For step-by-step guides on setting up these patterns, see the [Tutorials section](../../tutorials/).

In practice, that `env` often becomes a record. Records are easy to construct, easy to pass around,
and easy to understand at the boundary of an application.

FsFlow also supports these shapes:

- `RuntimeContext<'runtime, 'env>` gives your app services a separate lane from the host runtime
- a nominal capability puts a name on the dependency surface so the compiler can check it and
  helper signatures can advertise it
- standard `.NET` AppHost plus DI adapts the host container into a boundary record or nominal
  contract once, then workflow code stays on typed values

These are different shapes for different dependency boundaries. None of them is “more real” than
the others.

## Read In Order

| Step | Shape | When to use | Tutorial | Main APIs |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Environment + record | One record gives you an explicit boundary, direct field access, and easy construction | **[AppRecord](../../tutorials/app-record/)** | `Flow.env`, `Flow.read`, `Flow.localEnv` |
| 2 | [Nominal capability contracts](./capability-contracts/) | An interface through a capability contract puts the dependency surface in the type, so the compiler checks it and helpers advertise it | **[Capabilities](../../tutorials/capabilities/)** | interfaces, `Flow.read` |
| 3 | [RuntimeContext<'runtime, 'env>](./runtime-context/) | Two readers separate the host runtime from app services, so each side stays named and explicit | **[RuntimeContext](../../tutorials/runtime-context/)** | `RuntimeContext.create`, `Flow.readRuntime`, `Flow.readEnvironment` |
| 4 | [Standard .NET AppHost Plus DI](./provider-edge/) | Host registrations become a boundary record or nominal contract once, and workflow code stays on typed values | **[AppHost](../../tutorials/app-host/)** | `Resolver.fromProvider`, `MissingCapability` |

## Start With A Record

Records keep the boundary concrete. They are easy to read, easy to construct, and easy to pass
around.

```fsharp
type ApiDeps =
    { Orders : IOrderRepository
      Email : IEmailSender
      Clock : IClock }

let workflow : Flow<ApiDeps, string, OrderId> =
    flow {
        let! deps = Flow.env
        let! email = Flow.read _.Email
        let! created = deps.Orders.Create()
        do! email.SendConfirmation created
        return created.Id
    }
```

The record gives the flow a named boundary, and `Flow.read` keeps each access local to the piece
the flow actually needs.

Records start to get noisy when:

- every helper takes a bigger and bigger parameter bag
- you keep threading the same deps through unrelated steps
- the dependency surface matters more than the actual data
- the code wants to say “this effect needs logging, clock, and db” instead of “it needs a blob”

At that point a named cap gives the compiler a dependency surface to check and gives the reader a
sentence instead of a bag of fields.

## Support For Records

FsFlow supports records directly through the same `Flow` helpers:

- `Flow.env` to get the whole record
- `Flow.read` to project a single field or computed value
- `Flow.localEnv` to reshape the record for a smaller scope
- `Flow.provideLayer` when a downstream flow should run against a derived environment

That means records stay a direct, supported shape for dependency handling in `env`.

## When Caps Help

A capability contract puts a name on the dependency surface and keeps it in the type:

- it makes the effect’s requirements visible in the type
- it gives refactoring a boundary the compiler can check
- it behaves like programming to interfaces, but for effects
- it keeps flow code honest about what it depends on

That makes caps attractive even when a record would still work.

## More Direct Dependency Management

Under the hood, the runtime foundation does the heavy lifting:

- `Registry` stores tagged services and overrides.
- `Scope` registers finalizers and disposes them deterministically.
- `Layer` builds and composes runtime state.
- the adapter layer projects runtime state into nominal contracts.

That split keeps workflow code readable while still giving the host enough structure to manage
lifetimes and service selection.

## Choosing A Shape

The record shape gives you a concrete boundary and direct field access.

```fsharp
type ApiDeps =
    { Orders : IOrderRepository
      Email : IEmailSender
      Clock : IClock }
```

`RuntimeContext` gives your app services a separate lane from the host runtime.

Standard `.NET` AppHost plus DI adapts the container into a boundary record or nominal contract
once, then workflow code stays on typed values.

Nominal capability contracts let a helper or workflow name the capabilities it needs and give the
compiler a surface to check.

## Architectural Styles

All of these styles are still about `env`.

The question is not whether FsFlow uses `env`. The question is which shape makes the dependency
boundary easiest to read, test, and evolve.

### 1. Booted App Environment

This style keeps `env` as one concrete record that holds the dependencies for the whole app.

```fsharp
type AppEnv =
    { Db : IDb
      Log : string -> unit
      Config : AppConfig
      Billing : IBillingClient
      Cache : ICache }

let handle command : Flow<AppEnv, AppError, ResultValue> =
    flow {
        let! env = Flow.env
        env.Log "handling command"
        return! runWorkflow env.Db env.Billing command
    }
```

This style gives you:

- controllers and endpoints
- handlers
- persistence workflows
- infrastructure-heavy orchestration
- integration tests

The benefit is directness: the workflow sees one concrete application environment and the host
construction stays obvious.

### 2. Explicit Dependencies Plus Context

This style keeps the request or execution context small and passes the real dependencies as named
inputs.

```fsharp
type PlaceOrderDeps =
    { LoadCart : CartId -> Flow<RequestContext, AppError, Cart>
      SaveOrder : Order -> Flow<RequestContext, AppError, unit>
      PublishEvent : OrderPlaced -> Flow<RequestContext, AppError, unit> }

type RequestContext =
    { TraceId : string
      UserId : string
      Deadline : System.DateTimeOffset }

let placeOrder
    (deps : PlaceOrderDeps)
    (input : PlaceOrderInput)
    : Flow<RequestContext, AppError, OrderId> =
    flow {
        let! ctx = Flow.env
        let! cart = deps.LoadCart input.CartId
        let order = Order.create ctx.UserId cart
        do! deps.SaveOrder order
        do! deps.PublishEvent (OrderPlaced order.Id)
        return order.Id
    }
```

This style gives you:

- use cases
- domain orchestration
- workflow modules
- highly testable feature logic

The benefit is locality: each helper says what it needs, and the request or execution context stays
small and focused.

This style maps well to nominal capability contracts because the type itself carries the dependency
surface. It maps to a concrete record when the dependency list is local and obvious.

### 3. Standard `.NET` AppHost Plus DI

This style keeps the host conventional and turns registrations into a boundary record or nominal
contract once.

```fsharp
type RuntimeServices =
    { Logger : ILogger<ShipOrderWorkflow> }

type AppEnv =
    { Gateway : IShippingGateway }

type ShipOrderWorkflow() =
    member _.Run(input : ShipOrderInput) : Flow<RuntimeContext<RuntimeServices, AppEnv>, AppError, ShipmentId> =
        flow {
            let! logger = Flow.readRuntime _.Logger
            let! gateway = Flow.read _.Gateway

            logger.LogInformation("shipping order {OrderId}", input.OrderId)
            let! shipmentId = gateway.CreateShipment(input.OrderId)
            return shipmentId
        }
```

This style gives you:

- mixed C# and F# teams
- enterprise `.NET` apps
- incremental adoption

The benefit is that the host remains familiar while workflow code can still use explicit contracts
and `RuntimeContext` where they fit.

If the task boundary needs your app services separate from the host runtime, use
`RuntimeContext<'runtime, 'env>` and the `Flow.readRuntime` / `Flow.read` split instead of forcing
everything into one record.

This style maps directly onto standard `.NET` AppHost Plus DI because the host container can be
adapted into typed values once. It maps into nominal capability contracts when the workflow code
should speak in named capabilities rather than container lookups.

## How The Pieces Fit Together

The dependency story is:

- host registrations become runtime state at the edge
- runtime state is adapted into public contracts
- public workflows consume contracts, not lookup machinery
- `RuntimeContext` is the carrier for the host/runtime split
- `Resolver` is the host-edge binding surface, not the main application shape

That is the shape the rest of the dependency docs follow.
