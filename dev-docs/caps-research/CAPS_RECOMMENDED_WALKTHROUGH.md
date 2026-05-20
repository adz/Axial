# Recommended Capability Walkthrough

Status: research context. The live source of truth is `../PLAN.md`.

This walkthrough shows an older no-generator capability-level model from a new user's point of view. It contains
useful tradeoff examples, but its `RuntimeContext<'runtime, 'env>` sections are not the implemented public model.
Current FsFlow keeps runtime/system services ambient and outside end-user `'env` signatures.

The example is an order workflow. It logs a message, saves an order, sends an email, and returns the new order id.

## The Mental Model

Do not start by inventing a capability framework.

Start with the smallest thing that helps:

```text
Level 1: explicit dependency record
Level 2: RuntimeContext split into runtime + app
Level 3: IServiceProvider-backed runtime/app for DI-heavy hosts
Level 4: nominal IHasX helpers for reusable strict effects
```

The levels are additive. A real app can use more than one.

## Step 1: Define Your App Services

These are ordinary interfaces from your application.

```fsharp
type OrderCommand =
    { CustomerEmail : string
      ItemSku : string }

type Order =
    { Id : Guid
      CustomerEmail : string }

type IOrderRepository =
    abstract Save : OrderCommand -> Task<Result<Order, AppError>>

type IEmailSender =
    abstract SendConfirmation : Order -> Task<Result<unit, AppError>>
```

FsFlow does not own these. They are your application contracts.

## Level 1: Explicit Dependency Record

Use this first for small or feature-local workflows.

```fsharp
type SubmitOrderDeps =
    { Log : LogEntry -> unit
      Orders : IOrderRepository
      Email : IEmailSender }
```

Workflow:

```fsharp
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

This is close to argument passing. That is not a problem. It is clear, local, and type checked.

Build it directly in tests:

```fsharp
let deps =
    { Log = ignore
      Orders = fakeOrders
      Email = fakeEmail }
```

Build it from DI at the host edge:

```fsharp
module SubmitOrderDeps =
    let fromServiceProvider (sp: IServiceProvider) : SubmitOrderDeps =
        { Log =
            sp.GetRequiredService<ILoggerFactory>()
              .CreateLogger("Orders")
            |> RuntimeAdapters.logFromLogger

          Orders = sp.GetRequiredService<IOrderRepository>()
          Email = sp.GetRequiredService<IEmailSender>() }
```

Use this level when the workflow's dependencies are not reused widely.

### Adding A Layer

If the host starts with `IServiceProvider`, a layer can build the dependency record before the workflow runs.

```fsharp
let submitOrderLayer : TaskFlow<IServiceProvider, StartupError, SubmitOrderDeps> =
    taskFlow {
        let! sp = TaskFlow.env

        return
            { Log =
                sp.GetRequiredService<ILoggerFactory>()
                  .CreateLogger("Orders")
                |> RuntimeAdapters.logFromLogger

              Orders = sp.GetRequiredService<IOrderRepository>()
              Email = sp.GetRequiredService<IEmailSender>() }
    }
```

Then:

```fsharp
let program =
    submitOrder command
    |> TaskFlow.provideLayer submitOrderLayer
```

The workflow still sees `SubmitOrderDeps`. The app boundary only needs to provide `IServiceProvider`.

## Level 2: RuntimeContext Split

Use `RuntimeContext<'runtime, 'env>` when operational concerns should be separated from application services.

Runtime box:

```fsharp
type OrderRuntime =
    { Log : LogEntry -> unit
      Clock : IClock }
```

App box:

```fsharp
type OrderApp =
    { Orders : IOrderRepository
      Email : IEmailSender }
```

Workflow:

```fsharp
let submitOrder command : TaskFlow<RuntimeContext<OrderRuntime, OrderApp>, AppError, Guid> =
    taskFlow {
        let! log = Flow.readRuntime _.Log
        let! orders = Flow.readEnvironment _.Orders
        let! email = Flow.readEnvironment _.Email

        log
            { Level = LogLevel.Information
              Message = "Submitting order"
              TimestampUtc = DateTimeOffset.UtcNow }

        let! order = orders.Save command
        do! email.SendConfirmation order

        return order.Id
    }
```

Run it:

```fsharp
let context =
    RuntimeContext.create runtime app cancellationToken

let! result =
    submitOrder command
    |> TaskFlow.runContext context
```

Use this level when logging, clock, tracing, metrics, or provider access have a different lifetime or ownership
than domain services.

### Layering A RuntimeContext

A layer can build the full context:

```fsharp
let orderContextLayer : TaskFlow<IServiceProvider, StartupError, RuntimeContext<OrderRuntime, OrderApp>> =
    taskFlow {
        let! sp = TaskFlow.env
        let! ct = TaskFlow.Runtime.cancellationToken

        let runtime =
            { Log =
                sp.GetRequiredService<ILoggerFactory>()
                  .CreateLogger("Orders")
                |> RuntimeAdapters.logFromLogger

              Clock = sp.GetRequiredService<IClock>() }

        let app =
            { Orders = sp.GetRequiredService<IOrderRepository>()
              Email = sp.GetRequiredService<IEmailSender>() }

        return RuntimeContext.create runtime app ct
    }
```

Then:

```fsharp
let program =
    submitOrder command
    |> TaskFlow.provideLayer orderContextLayer
```

The outer app supplies `IServiceProvider`; the inner workflow receives `RuntimeContext<OrderRuntime, OrderApp>`.

## Level 3: IServiceProvider-Backed Runtime

Use this when the app is already DI-first and boilerplate matters.

Runtime carries operational adapters and provider access:

```fsharp
type AppRuntime =
    { Log : LogEntry -> unit
      Clock : IClock
      Services : IServiceProvider }
```

The app environment can be the provider:

```fsharp
type AppContext =
    RuntimeContext<AppRuntime, IServiceProvider>
```

Service lookup is explicit:

```fsharp
module Service =
    let get<'service> : TaskFlow<RuntimeContext<'runtime, IServiceProvider>, MissingCapability, 'service> =
        taskFlow {
            let! sp = Flow.readEnvironment id

            match sp.GetService typeof<'service> with
            | null ->
                return!
                    TaskFlow.error
                        { CapabilityType = typeof<'service> }

            | value ->
                return unbox<'service> value
        }
```

Workflow:

```fsharp
let submitOrder command =
    taskFlow {
        do! Log.info "Submitting order"

        let! orders = Service.get<IOrderRepository>
        let! email = Service.get<IEmailSender>

        let! order = orders.Save command
        do! email.SendConfirmation order

        return order.Id
    }
```

Build runtime from the host:

```fsharp
module RuntimeAdapters =
    let fromServiceProvider (sp: IServiceProvider) : AppRuntime =
        { Log =
            sp.GetRequiredService<ILoggerFactory>()
              .CreateLogger("FsFlow")
            |> RuntimeAdapters.logFromLogger

          Clock = sp.GetRequiredService<IClock>()
          Services = sp }
```

Run it:

```fsharp
let runtime =
    RuntimeAdapters.fromServiceProvider app.Services

let context =
    RuntimeContext.create runtime app.Services cancellationToken

let! result =
    submitOrder command
    |> TaskFlow.runContext context
```

This is not as statically honest as Level 1 or Level 4. The type tells you the workflow needs
`IServiceProvider`, not exactly `IOrderRepository` and `IEmailSender`.

The benefit is real:

```text
low boilerplate
excellent ASP.NET/Aspire/worker fit
uses existing DI registrations
can use startup validation
```

Layers are optional here. The host's DI container already acts like a layer system. Add an FsFlow layer only when
you want to validate or adapt the provider before running the workflow.

## Runtime Adapters

Adapters let FsFlow keep generic runtime semantics while still using host-native services.

Example: adapt `ILogger` into FsFlow logging:

```fsharp
module RuntimeAdapters =
    let logFromLogger (logger: ILogger) : LogEntry -> unit =
        fun entry ->
            logger.Log(
                mapLevel entry.Level,
                "{Message}",
                entry.Message)
```

This keeps the core contract:

```fsharp
LogEntry -> unit
```

while allowing ASP.NET code to provide:

```fsharp
ILogger
```

## Level 4: Nominal IHasX Helpers

Use this when an operation is reused widely and static requirements are worth the ceremony.

Define small capability interfaces:

```fsharp
type IHasOrders =
    abstract Orders : IOrderRepository

type IHasEmail =
    abstract Email : IEmailSender
```

Concrete app record:

```fsharp
type OrderApp =
    { OrdersValue : IOrderRepository
      EmailValue : IEmailSender }

    interface IHasOrders with
        member x.Orders = x.OrdersValue

    interface IHasEmail with
        member x.Email = x.EmailValue
```

Helper modules:

```fsharp
module Orders =
    let repository () : TaskFlow<RuntimeContext<'runtime, #IHasOrders>, 'error, IOrderRepository> =
        Flow.readEnvironment _.Orders

module Email =
    let sender () : TaskFlow<RuntimeContext<'runtime, #IHasEmail>, 'error, IEmailSender> =
        Flow.readEnvironment _.Email
```

Workflow:

```fsharp
let submitOrder command =
    taskFlow {
        do! Log.info "Submitting order"

        let! orders = Orders.repository ()
        let! email = Email.sender ()

        let! order = orders.Save command
        do! email.SendConfirmation order

        return order.Id
    }
```

Conceptually, the workflow has this requirement:

```text
TaskFlow<RuntimeContext<runtime, env>, AppError, Guid>
  when runtime :> IHasLog
   and env :> IHasOrders
   and env :> IHasEmail
```

If the app environment does not implement `IHasEmail`, the workflow cannot run with that context.

Use this level for shared libraries or reusable workflow modules. Do not use it for every small feature by default.

### Layering Nominal Capabilities

The layer builds the concrete record that implements the `IHasX` interfaces:

```fsharp
let orderAppLayer : TaskFlow<IServiceProvider, StartupError, OrderApp> =
    taskFlow {
        let! sp = TaskFlow.env

        return
            { OrdersValue = sp.GetRequiredService<IOrderRepository>()
              EmailValue = sp.GetRequiredService<IEmailSender>() }
    }
```

If the workflow expects `RuntimeContext<'runtime, OrderApp>`, layer into that context at the boundary.

## Choosing The Level

Start here:

```text
Can a feature dependency record express this cleanly?
  Use Level 1.
```

Move up when there is a reason:

```text
Need runtime/app separation?
  Use Level 2.

Already in DI-heavy ASP.NET/Aspire/worker code?
  Use Level 3.

Building reusable operations where static dependency proof matters?
  Use Level 4.
```

## Which Level Works Best For LLM Coding?

For code that coding agents will read and edit, prefer the shapes with the fewest hidden moving parts.

Best default:

```text
Level 1 explicit dependency records.
```

Agents can see all dependencies in one type:

```fsharp
type SubmitOrderDeps =
    { Log : LogEntry -> unit
      Orders : IOrderRepository
      Email : IEmailSender }
```

When an agent adds a dependency, the edit is mechanical:

```text
add one field
read/use the field in the workflow
update the test construction
update the DI adapter if one exists
```

Good second choice:

```text
Level 2 RuntimeContext with concrete named records.
```

This is still easy because the split is visible:

```fsharp
type OrderRuntime =
    { Log : LogEntry -> unit }

type OrderApp =
    { Orders : IOrderRepository
      Email : IEmailSender }
```

Use Level 3 `IServiceProvider` near host boundaries. It is ergonomic, but agents must remember registrations and
lifetimes. Keep provider-backed code close to handlers, jobs, and app startup.

Use Level 4 nominal helpers only when the pattern is already established in the codebase. It is good for reusable
library-style helpers, but it asks an agent to coordinate interfaces, records, helper modules, adapters, and tests.

Agent-friendly default:

```text
Prefer explicit records in feature code.
Use RuntimeContext records when runtime/app split matters.
Use IServiceProvider-backed access near the host.
Use nominal IHasX helpers only for shared reusable effects.
```

## End State

The recommended FsFlow capability story is:

```text
Do not force a capability framework.
Use explicit records when they are enough.
Use IServiceProvider-backed runtime when host ergonomics matter.
Use nominal IHasX helpers only where reusable static requirements pay for themselves.
Use adapters to move between records, providers, and runtime capabilities.
```
