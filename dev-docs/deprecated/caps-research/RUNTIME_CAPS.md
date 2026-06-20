Here’s a **clean, complete design brief** you can drop into a fresh LLM session.

> Superseded research note: this brief reflects an older `RuntimeContext`-centric design and is no longer the target
> v1 architecture.

---

# Axial Design: RuntimeContext + Env + Policy-based CE

## 🎯 Goal

Build a **ZIO-like effect system in idiomatic F#** that:

* Keeps **clean CE ergonomics**
* Supports **runtime policies (retry, timeout, tracing, metrics)**
* Separates:

  * **User/business dependencies** (Env)
  * **Operational/runtime services** (RuntimeContext)
* Integrates with:

  * `IServiceProvider` (AppHost / ASP.NET style)
  * or pure F# record-based environments

---

# 🧠 Core Model

```fsharp
TaskFlow<'env, 'err, 'a>
```

Conceptually:

```fsharp
RuntimeContext -> 'env -> Task<Result<'a, 'err>>
```

---

# 🧱 Two Contexts (CRITICAL DESIGN)

## 1. RuntimeContext (system-provided)

**Purpose:** operational concerns, always available

```fsharp
type RuntimeContext =
    {
        Logger : ILogger
        Metrics : IMetrics
        Tracer : ITracer
        CancellationToken : CancellationToken
        Clock : IClock
        Annotations : Map<string, obj>
        Services : IServiceProvider option
    }
```

### Used by:

* `log`, `logWith`
* `measure`
* `withSpan`
* `annotate`
* `sleep`, `utcNow`
* `cancellationToken`, `ensureNotCanceled`
* `retry`, `timeout`

👉 These are **not user dependencies**

---

## 2. Env (user-provided)

**Purpose:** application/business dependencies

```fsharp
type AppEnv =
    {
        Db : IDb
        Http : IHttpClient
        DeviceClient : IDeviceClient
        Config : Config
    }
```

### Accessed via:

```fsharp
let! db = read _.Db
```

---

# ⚖️ Separation Rule

```text
RuntimeContext = HOW the system runs
Env            = WHAT the app needs
```

---

# 🧩 Computation Expression Design

## Plan-based CE (required for block policies)

The CE builds a **plan**, not a final flow.

```fsharp
type TaskFlowSpec<'env,'err,'a> =
    {
        Policies : RuntimePolicy list
        Build : unit -> TaskFlow<'env,'err,'a>
    }
```

### Run:

```fsharp
member _.Run spec =
    spec.Build()
    |> applyPolicies spec.Policies
```

---

# 🔁 Runtime Policies (BLOCK-LEVEL)

## Custom Operations

```fsharp
taskFlow {
    withRetry retryPolicy
    withTimeout (TimeSpan.FromSeconds 10.0) TimeoutError
    withSpan "device.read"
    annotate "deviceId" deviceId

    let! result = callDevice deviceId
    return result
}
```

### Semantics:

```fsharp
taskFlow { ... }
|> Runtime.withRetry retryPolicy
|> Runtime.withTimeout ...
|> Runtime.withSpan ...
|> Runtime.annotate ...
```

---

## Nested Scoping

```fsharp
taskFlow {
    let! a = stepA()

    let! b =
        taskFlow {
            withRetry retryB
            withTimeout shortTimeout Timeout

            let! x = flakyCall a
            return x
        }

    return b
}
```

👉 Inner policies apply ONLY to inner block

---

# 🧪 Action Custom Ops (CE-local effects)

These operate as **statements inside CE**

```fsharp
taskFlow {
    do! log Info "Starting"
    do! sleep (TimeSpan.FromMilliseconds 100.0)
    do! ensureNotCanceled Cancelled

    let! ct = cancellationToken
}
```

### These come from RuntimeContext

---

# 📏 `measure` Design

```fsharp
taskFlow {
    measure "device.poll.duration"

    let! status = readStatus deviceId
    return status
}
```

### Behavior:

* Starts timer before block
* Stops on success/failure/cancel
* Emits metric with annotations

---

# 🏷️ `annotate` Design

```fsharp
taskFlow {
    annotate "deviceId" deviceId
    annotate "protocol" "modbus"

    do! log Info "Reading device"
}
```

### Behavior:

* Adds to RuntimeContext.Annotations
* Automatically flows into:

  * logs
  * metrics
  * spans

---

# 🔍 Logging Design

```fsharp
taskFlow {
    do! log Info "Polling"
}
```

### Logger comes from:

```fsharp
RuntimeContext.Logger
```

NOT from Env

---

# ⏱️ Retry / Timeout Design

## Block-level (preferred)

```fsharp
taskFlow {
    withRetry retryPolicy
    withTimeout timeout error

    let! x = work
    return x
}
```

## Sub-flow (via nesting)

```fsharp
let! x =
    taskFlow {
        withRetry retryPolicy
        let! r = flaky()
        return r
    }
```

## Function form (fallback)

```fsharp
flow |> Runtime.retry policy
```

---

# 🧱 Layer / EnvBuilder (Dependency Wiring)

## Layer Type

```fsharp
type Layer<'inEnv, 'err, 'outEnv> =
    TaskFlow<'inEnv, 'err, 'outEnv>
```

---

## Example EnvBuilder

```fsharp
let appLayer =
    envBuilder {
        let! config = read id
        let! db = Db.connect config.ConnectionString
        let http = Http.create config.BaseUrl

        return {
            Db = db
            Http = http
            Config = config
        }
    }
```

---

## Providing Layer

```fsharp
program
|> TaskFlow.provideLayer appLayer
```

Transforms:

```text
Config -> Task<Result<AppEnv, StartupError>>
AppEnv -> Task<Result<'a, Error>>
```

into:

```text
Config -> Task<Result<'a, Error>>
```

---

# 🔌 AppHost Integration (IServiceProvider)

RuntimeContext optionally contains:

```fsharp
Services : IServiceProvider option
```

---

## Access pattern

```fsharp
let! service =
    resolve<IMyService>()
```

Where:

```fsharp
let resolve<'T> () =
    taskFlow {
        let! sp = runtimeServices
        return sp.GetRequiredService<'T>()
    }
```

---

## Alternative: convert IServiceProvider → Env

```fsharp
let layerFromServices =
    envBuilder {
        let! sp = runtimeServices

        return {
            Db = sp.GetRequiredService<IDb>()
            Http = sp.GetRequiredService<IHttpClient>()
        }
    }
```

---

# 🧠 Naming Conventions

## Block policies

```text
withRetry
withTimeout
withSpan
annotate
measure
```

👉 “decorate this block”

---

## Action ops

```text
log
sleep
ensureNotCanceled
cancellationToken
utcNow
```

👉 “execute this effect”

---

## Module functions

```text
map, bind, traverse, sequence
retry, timeout (function form)
provideEnv, provideLayer
```

👉 composition, pipelines

---

# 🎯 Final Design Rules

## 1. Two-context model

```text
RuntimeContext = system services
Env            = app dependencies
```

---

## 2. Custom ops ONLY for:

* block policies
* CE-local actions

NOT for:

* map/bind/traverse
* general composition

---

## 3. Nested CE = scoped policy

```fsharp
taskFlow {
    let! x =
        taskFlow {
            withRetry ...
            ...
        }
}
```

---

## 4. Prefer block ops over inline wrapping

```fsharp
withRetry policy   ✅
retry policy flow  ⚠️ fallback
```

---

## 5. RuntimeContext drives observability

* logging
* metrics
* tracing
* cancellation

---

# 🚀 Result

You get:

```fsharp
taskFlow {
    withRetry networkRetry
    withTimeout (TimeSpan.FromSeconds 10.0) Timeout
    withSpan "device.read"
    annotate "deviceId" deviceId

    do! log Info "Reading device"

    let! client = read _.DeviceClient
    let! result = client.Read deviceId

    return result
}
```

Which is:

* minimal syntax
* fully typed
* composable
* observable
* environment-driven
* production-ready

---

Yes. This could become the real model:

```fsharp
taskFlow {
    useServices {
        service logger
        service metrics
        service deviceClient
    }

    withRetry networkRetry
    measure "device.poll"
    annotate "deviceId" deviceId

    do! log Info "Polling"

    let! client = service<IDeviceClient>
    return! client.Read deviceId
}
```

But I’d separate **three concepts**.

## 1. Service access inside a flow

This is just Reader/capability access with better naming:

```fsharp
taskFlow {
    let! db = service<IDb>
    let! clock = service<IClock>
    let! devices = service<IDeviceClient>

    return! devices.Read deviceId
}
```

Implementation shape:

```fsharp
type Runtime =
    {
        Services : IServiceProvider
        Logger : ILogger
        Metrics : IMetrics
        Clock : IClock
        Annotations : Map<string, obj>
    }

let service<'a> : TaskFlow<Runtime, Error, 'a> =
    fun rt -> task {
        match rt.Services.GetService(typeof<'a>) with
        | :? 'a as value -> return Ok value
        | _ -> return Error (MissingService typeof<'a>)
    }
```

So `service<'a>` is equivalent to ZIO-style “requires capability `'a`”.

## 2. Service provision

Users provide capabilities at the edge:

```fsharp
let runtime =
    runtime {
        useLogger logger
        useMetrics metrics
        useClock SystemClock

        service<IDb> db
        service<IDeviceClient> deviceClient
    }

TaskFlow.run runtime program
```

That `runtime { ... }` CE is not the same as `taskFlow`. It builds the runtime/capability bag.

Shape:

```fsharp
type RuntimeBuilderState =
    {
        Services : Map<Type, obj>
        Logger : ILogger option
        Metrics : IMetrics option
        Clock : IClock option
    }
```

Custom ops mutate/build the state:

```fsharp
runtime {
    useLogger logger
    service<IDb> db
}
```

becomes conceptually:

```fsharp
Runtime.empty
|> Runtime.withLogger logger
|> Runtime.addService<IDb> db
```

F# custom operations are CE-specific names that come into scope inside `{ ... }`; newer F# also supports parameterless `[<CustomOperation>]`, which makes declaring these less noisy. ([Microsoft Learn][1])

## 3. Layers

A `Layer` is a recipe for building services, possibly from other services/config:

```fsharp
let deviceLayer =
    layer {
        let! config = service<DeviceConfig>
        let! logger = service<ILogger>

        let client = DeviceClient(config, logger)

        provide<IDeviceClient> client
    }
```

Then:

```fsharp
let runtime =
    runtime {
        service<DeviceConfig> config
        useLogger logger

        layer deviceLayer
    }
```

So:

```text
runtime CE = assemble concrete runtime
layer CE   = reusable recipe for adding capabilities
taskFlow   = program that consumes capabilities
```

## AppHost / Aspire interop

This maps nicely to AppHost/Aspire.

Aspire AppHost is already an orchestration project that defines app resources, services, databases, containers, and relationships in code; Microsoft’s starter structure includes an AppHost project and shared ServiceDefaults project. ([Microsoft Learn][2])

So Axial should **not replace AppHost**. It should consume what AppHost/Generic Host already builds.

Interop target:

```fsharp
let runtime =
    Runtime.fromServiceProvider app.Services
    |> Runtime.withLoggerFactory loggerFactory
    |> Runtime.withMeterFactory meterFactory
```

Then in app code:

```fsharp
app.MapPost("/poll", Func<PollRequest, Task<IResult>>(fun req ->
    task {
        let flow =
            taskFlow {
                measure "device.poll"
                annotate "deviceId" req.DeviceId

                let! client = service<IDeviceClient>
                let! result = client.Read req.DeviceId

                return result
            }

        let! result = TaskFlow.run runtime flow
        return toHttpResult result
    }))
```

Even better, DI registration:

```fsharp
builder.Services.AddAxialRuntime(fun rt ->
    rt.UseLoggerFactory()
      .UseOpenTelemetry()
      .UseSystemClock()
      .UseServiceProvider())
```

Then handlers get:

```fsharp
type PollHandler(runtime: AxialRuntime) =

    member _.Poll req =
        taskFlow {
            measure "device.poll"
            annotate "deviceId" req.DeviceId

            let! client = service<IDeviceClient>
            return! client.Read req.DeviceId
        }
        |> TaskFlow.run runtime
```

## Important design choice

I would **not** make everything use `IServiceProvider` internally by default.

Best split:

```fsharp
type Runtime<'services> =
    {
        Core : CoreRuntime
        Services : 'services
    }
```

Then F# users can use records:

```fsharp
type AppServices =
    {
        Db : IDb
        DeviceClient : IDeviceClient
    }

taskFlow {
    let! client = service _.DeviceClient
    return! client.Read id
}
```

And AppHost/ASP.NET users can use DI:

```fsharp
taskFlow {
    let! client = service<IDeviceClient>
    return! client.Read id
}
```

So expose both:

```fsharp
service _.DeviceClient      // typed record capability, best F#
service<IDeviceClient>      // IServiceProvider style, best AppHost/ASP.NET interop
```

Final shape:

```fsharp
taskFlow {
    withRetry networkRetry
    withTimeout (TimeSpan.FromSeconds 5.0) TimedOut
    measure "device.poll"
    annotate "deviceId" deviceId

    do! log Info "Polling"

    let! client = service<IDeviceClient>
    return! client.Read deviceId
}
```

That is the exciting version: Axial becomes a **typed effect runtime + capability DSL**, not merely `Task<Result<_,_>>` with Reader.
