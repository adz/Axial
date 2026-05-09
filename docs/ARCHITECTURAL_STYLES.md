---
weight: 40
title: Architectural Styles
---

# Architectural Styles

This page shows how the FsFlow workflow family should fit into your application architecture without forcing one app shape on every codebase.

FsFlow supports three valid architectural styles.

> Three related workflow families. Three valid architectural styles. Choose based on app shape and team preferences.

## 1. Booted App Environment

Use this when your application already has a conventional booted runtime and passing one explicit application environment keeps composition simple.

This is the most direct use of the [Record Pattern](./env-slicing/). The environment is the full booted application runtime and config:

- DB
- logging
- config
- clients
- services
- cache

Typical shape:

```fsharp
type AppEnv =
    { Db: IDb
      Log: string -> unit
      Config: AppConfig
      Billing: IBillingClient
      Cache: ICache }

let handle command : Flow<AppEnv, AppError, ResultValue> =
    flow {
        let! env = Flow.env
        env.Log "handling command"
        return! runWorkflow env.Db env.Billing command
    }
```

This is inspired by app-level runtime shapes such as Rails, Phoenix, or a booted `.NET` host,
but the environment is still passed explicitly and never global.

Use this style for:

- controllers and endpoints
- handlers
- persistence workflows
- infrastructure-heavy orchestration
- integration tests

Choose this when simplicity of app composition matters most.

## 2. Explicit Dependencies Plus Context

Use this when you want feature modules to state their real dependencies directly and keep `'ctx`
for request or execution context only.

Typical shape:

```fsharp
type PlaceOrderDeps =
    { LoadCart: CartId -> Flow<RequestContext, AppError, Cart>
      SaveOrder: Order -> Flow<RequestContext, AppError, unit>
      PublishEvent: OrderPlaced -> Flow<RequestContext, AppError, unit> }

type RequestContext =
    { TraceId: string
      UserId: string
      Deadline: System.DateTimeOffset }

let placeOrder
    (deps: PlaceOrderDeps)
    (input: PlaceOrderInput)
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

The shape is:

```fsharp
deps -> input -> Flow<'ctx, 'err, 'a>
```

Here, the [Record Pattern](./env-slicing/) is used only for the thin `'ctx` (request or execution context). Typical examples:

- trace id
- request id
- user or session
- cancellation
- deadline

Use this style for:

- use cases
- domain orchestration
- workflow modules
- highly testable feature logic

Choose this when clarity and locality matter most.

## 3. Standard `.NET` AppHost Plus DI

Use this when the surrounding application should stay in standard `.NET` startup and dependency
injection, and FsFlow should only appear inside workflow code.

Keep the host conventional:

- `AppHost`
- DI container
- logging, config, and options
- normal `.NET` startup

Use FsFlow inside:

- feature workflows
- handlers
- jobs
- application services

Typical shape:

```fsharp
type RuntimeServices =
    { Logger: ILogger<ShipOrderWorkflow> }

type AppEnv =
    { Gateway: IShippingGateway }

type ShipOrderWorkflow() =
    member _.Run(input: ShipOrderInput) : TaskFlow<RuntimeContext<RuntimeServices, AppEnv>, AppError, ShipmentId> =
        taskFlow {
            let! logger = TaskFlow.readRuntime _.Logger
            let! gateway = TaskFlow.read _.Gateway

            logger.LogInformation("shipping order {OrderId}", input.OrderId)
            let! shipmentId = gateway.CreateShipment(input.OrderId)
            return shipmentId
        }
```

This style often utilizes the [CAPS Pattern](./capabilities/) to bridge existing .NET services into a decoupled FsFlow contract.

Use this style for:

- mixed C# and F# teams
- enterprise `.NET` apps
- incremental adoption

Choose this when familiarity and low migration risk matter most.

If the task boundary needs separate runtime services and application capabilities, use
`RuntimeContext<'runtime, 'env>` and the `TaskFlow.readRuntime` / `TaskFlow.read` split instead of
forcing everything into one record.

## Which Style To Prefer

There is no single mandated architecture.

- Use Booted App Environment when app-level composition is the main concern.
- Use Explicit Dependencies Plus Context when feature-level reasoning and testability matter more.
- Use standard `.NET` AppHost plus DI when the host should stay conventional and the workflow family is an internal application abstraction.

The important constraint is not which style you pick.
The important constraint is that the chosen workflow family stays explicit at the workflow boundary and at execution time.

## Next

Read [`docs/GETTING_STARTED.md`](../start/getting-started/) for the core workflow model,
[`docs/ENV_SLICING.md`](./env-slicing/) for smaller environment projections, and
[`docs/WHY_FSFLOW.md`](./why-fsflow/) for the trade-offs against manual
threading and wrapper-based shapes.
