# Recommended Capability Model

Status: research context. The live source of truth is `../PLAN.md`.

> Superseded research note: this model still explains some tradeoffs well, but its ambient-core and
> `RuntimeContext<'runtime, 'env>` direction is no longer the target v1 architecture.

This was the compressed recommendation for Axial capabilities without generators. It still helps explain records,
provider edges, and nominal helper tradeoffs, but its `RuntimeContext<'runtime, 'env>` examples are not the current
implemented public model. The active model keeps runtime/system services ambient and outside end-user `'env`
signatures.

## One Sentence

Use Axial levels: explicit records for local clarity, ambient runtime helpers for system services,
`IServiceProvider` at host edges, nominal helpers for reusable strict app dependencies, and adapters between those
shapes.

## The Levels

```text
Level 1: Explicit dependency records
  Best for small workflows, tests, and feature-local code.
  Lowest ceremony and best IDE clarity.

Level 2: RuntimeContext<'runtime, 'env>
  Best when operational concerns and app dependencies deserve separate ownership.
  'runtime = logging, clock, metrics, tracing, provider access.
  'env     = app/domain dependencies.

Level 3: Provider-backed runtime/app
  Best for ASP.NET, Aspire, workers, jobs, and DI-heavy apps.
  IServiceProvider can be a first-class runtime/app capability source.
  The tradeoff is runtime/startup validation instead of full compile-time proof.

Level 4: Nominal capability helpers
  Best for reusable operations shared across many workflows and environments.
  Small IHasX interfaces give compile-time checked requirements.
  Use only where the static benefit beats the boilerplate.
```

## How The Levels Fit

```text
Small feature
  TaskFlow<SubmitOrderDeps, AppError, Guid>

App-host feature
  TaskFlow<RuntimeContext<AppRuntime, IServiceProvider>, AppError, Guid>

Strict reusable helper
  TaskFlow<RuntimeContext<#IHasLog, #IHasOrders>, AppError, Order>

Adapters
  IServiceProvider -> explicit deps record
  IServiceProvider -> nominal capability record
  explicit deps record -> nominal capability interfaces
  ILogger / IClock / Meter -> Axial runtime capabilities
```

This avoids pretending there is one perfect capability model.

## Where Layers Fit

Layers are not a fifth architecture style. They are the way to build or adapt an environment before a workflow
runs.

In Axial terms, a layer is:

```fsharp
TaskFlow<'input, 'error, 'environment>
```

and `provideLayer` turns:

```text
Layer:   input -> environment
Program: environment -> value
```

into:

```text
Program: input -> value
```

So layers answer:

```text
Given what I have at the boundary, how do I build the environment this workflow wants?
```

They do not answer:

```text
How should every workflow express dependencies?
```

That remains a choice between records, `RuntimeContext`, provider-backed access, and nominal helpers.

Mapped to `docs/ARCHITECTURAL_STYLES.md`:

```text
Booted App Environment
  Layers build the booted AppEnv from config, service provider, secrets, connections, caches.
  After boot, most workflows just read AppEnv.

Explicit Dependencies Plus Context
  Layers usually live outside the feature workflow.
  They build the dependency record once; the workflow still has shape deps -> input -> Flow<'ctx,_,_>.

Standard .NET AppHost Plus DI
  The host DI container is already a layer system.
  Axial layers/adapters convert IServiceProvider into RuntimeContext, feature records, or nominal capability records.
```

Mapped to the capability levels:

```text
Level 1 explicit records
  A layer can build SubmitOrderDeps from config or IServiceProvider.

Level 2 RuntimeContext
  A layer can build RuntimeContext<runtime, env> from bootstrap input.

Level 3 provider-backed runtime
  The provider itself may be the app environment.
  Layers are optional adapters for startup validation or richer runtime records.

Level 4 nominal helpers
  A layer can build the concrete record that implements IHasX interfaces.
```

Rule of thumb:

```text
Use layers at composition boundaries.
Use records/provider/nominal helpers inside the workflow dependency model.
```

## What IServiceProvider Is Good For

`IServiceProvider` is not just an awkward edge escape hatch. It is the native dependency model for a lot of .NET
applications.

It is good for:

```text
ASP.NET handlers
background workers
hosted jobs
Aspire / AppHost integration
standard Microsoft.Extensions.* infrastructure
teams already invested in DI registration and validation
```

Axial should support it directly, but honestly:

```text
Provider-backed flows are ergonomic and host-native.
They are not fully statically honest about every service registration.
```

## Provider-Backed Shape

The runtime half can carry operational services and provider access:

```fsharp
type AppRuntime =
    { Log : LogEntry -> unit
      Clock : IClock
      Services : IServiceProvider }
```

The app half can be the provider itself:

```fsharp
type AppContext =
    RuntimeContext<AppRuntime, IServiceProvider>
```

Provider access is then an explicit Axial operation:

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

Benefit:

```text
Very low boilerplate.
Excellent host integration.
Dependencies are visible in code.
```

Cost:

```text
Missing services are runtime/startup validation failures, not type errors.
The workflow type says IServiceProvider, not IOrderRepository + IEmailSender.
```

## Runtime Adapters

Runtime adapters are the key to making provider-backed flows feel good without hard-wiring Microsoft types into
Axial core.

Example: adapt `ILogger` into Axial's generic `LogEntry -> unit` shape:

```fsharp
module RuntimeAdapters =
    let logFromLogger (logger: ILogger) : LogEntry -> unit =
        fun entry ->
            logger.Log(
                mapLevel entry.Level,
                "{Message}",
                entry.Message)

    let fromServiceProvider (sp: IServiceProvider) : AppRuntime =
        { Log =
            sp.GetRequiredService<ILoggerFactory>()
              .CreateLogger("Axial")
            |> logFromLogger

          Clock =
            sp.GetRequiredService<IClock>()

          Services = sp }
```

This gives a clean separation:

```text
Axial core owns LogEntry and runtime helper semantics.
Hosting package owns ILogger / IServiceProvider adapters.
Application host owns registrations.
```

## Explicit Record Shape

For small workflows, do this first:

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

This is basically argument passing with better composition. That is fine. Do not add a capability framework when a
record is clearer.

Provider adapter:

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

## Nominal Helper Shape

Use nominal capabilities when a helper is reused across many workflows or when the type-level requirement matters.

```fsharp
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

Helpers:

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

Conceptual requirement:

```text
runtime must provide log
env must provide orders and email
```

This buys compile-time honesty, but it costs boilerplate. Use it selectively.

## Choosing A Level

Use explicit records when:

```text
the workflow is feature-local
the dependency shape is small
you want the clearest IDE/tooling experience
```

Use provider-backed runtime when:

```text
the app is already DI-first
the workflow is close to ASP.NET / worker / host boundaries
startup validation is good enough
low boilerplate matters more than type-level service lists
```

Use nominal helpers when:

```text
the operation is shared across many workflows
you want the compiler to prove the dependency is available
the boilerplate is paid once and reused often
```

## LLM Coding Guidance

LLM coding agents do best with dependency shapes that are local, explicit, and easy to modify without understanding
the whole app. That changes the default recommendation.

Preferred order for agent-friendly code:

```text
1. Explicit dependency records
2. RuntimeContext with named runtime/app records
3. IServiceProvider-backed runtime close to host boundaries
4. Nominal IHasX helpers only when already established
```

### Best For LLMs: Explicit Records

Explicit records are easiest for agents to edit correctly:

```fsharp
type SubmitOrderDeps =
    { Log : LogEntry -> unit
      Orders : IOrderRepository
      Email : IEmailSender }
```

Why this works well:

```text
all dependencies are visible in one small type
adding a dependency is one field plus one construction-site update
tests are obvious
no hidden DI registration knowledge is needed
compiler errors point at concrete fields
```

This should be the default for examples, docs, and feature-local code that you expect coding agents to modify.

### Good For LLMs: RuntimeContext With Named Records

This is also manageable when the records are concrete:

```fsharp
type OrderRuntime =
    { Log : LogEntry -> unit
      Clock : IClock }

type OrderApp =
    { Orders : IOrderRepository
      Email : IEmailSender }
```

Why it works:

```text
the runtime/app split is visible
field names remain concrete
agents can add runtime concerns without touching app dependencies
```

Avoid making `'runtime` or `'env` too abstract in examples aimed at agents. Prefer named records until reuse forces
a more general shape.

### Mixed For LLMs: IServiceProvider-Backed Runtime

Provider-backed code is ergonomic, but agents can miss registrations:

```fsharp
let! orders = Service.get<IOrderRepository>
```

This is reasonable near ASP.NET/Aspire/worker boundaries because the host already thinks in DI. It is weaker deep
inside feature code because an agent must update:

```text
the workflow
DI registration
startup validation or tests
possibly service lifetimes
```

If using provider-backed flows, keep them agent-friendly by adding:

```text
one obvious module for Service.get
startup validation tests
examples of required registrations
small handler-level workflows rather than deep domain workflows
```

### Hardest For LLMs: Nominal IHasX Helpers

Nominal helpers are statically nice but spread edits across more places:

```text
IHasX interface
concrete record implementation
helper module
construction layer/adapter
workflow usage
```

Use them when the helper already exists or when a capability is reused widely enough to justify the pattern. Do not
expect agents to invent this structure correctly in one pass unless the repo has strong examples nearby.

### Agent-Friendly Rule

For code that LLMs will maintain, choose the least abstract shape that still protects the boundary:

```text
feature code: explicit records
runtime split: named RuntimeContext records
host integration: IServiceProvider-backed adapters
shared library helpers: nominal IHasX
```

This is another reason not to make nominal capabilities the universal default.

## Recommended 1.0 Position

Axial should not present “capabilities” as one mandatory framework.

Ship:

```text
Axial.Core
  TaskFlow, RuntimeContext, read/readRuntime/readEnvironment, cancellation mechanics.

Axial.Runtime
  Generic runtime helper contracts and operations: log, clock, metrics, tracing, annotations.

Axial.Hosting
  IServiceProvider-backed runtime/app adapters, service lookup helpers, startup validation helpers.

Docs
  Level 1 explicit records first.
  Level 2 RuntimeContext when runtime/app split matters.
  Level 3 IServiceProvider-backed flows for host-native apps.
  Level 4 nominal helpers for reusable strict capabilities.
```

Do not ship:

```text
source generators
SRTP structural accessors as the primary path
a fixed concrete RuntimeContext with every service baked in
a capability framework that is just argument passing with more ceremony
```
