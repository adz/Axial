Yes — your concern is right.

> Superseded research note: this file argues for extensible ambient runtime capabilities. That is no longer the target
> v1 architecture after the explicit-services-and-layers redesign.

The design is strongest if Axial keeps **three layers**:

1. **Core runtime capabilities**: logging, metrics, tracing, clock, cancellation, annotations.
2. **User services/env**: record-based or `IServiceProvider`.
3. **Extensible capabilities**: smaller typed slices users can define.

The risky bit in the current plan is that `RuntimeContext` can become a blessed kitchen sink. Once `Logger`, `Metrics`, `Tracer`, `Clock`, etc. are hardcoded, Axial is ZIO-ish, but not very *capability-extensible*. Your brief already separates `RuntimeContext = HOW the system runs` from `Env = WHAT the app needs` , but I’d push that further.

Better shape:

```fsharp
type CoreRuntime =
    {
        CancellationToken : CancellationToken
        Annotations : Map<string, obj>
        Services : IServiceProvider option
    }

type Runtime<'caps> =
    {
        Core : CoreRuntime
        Capabilities : 'caps
    }
```

Then Axial can ship small capability records:

```fsharp
type LoggingCap =
    { Logger : ILogger }

type MetricsCap =
    { Metrics : IMetrics }

type ClockCap =
    { Clock : IClock }
```

And users can compose:

```fsharp
type MyRuntimeCaps =
    {
        Logging : LoggingCap
        Metrics : MetricsCap
        Clock : ClockCap
        DeviceOps : DeviceOpsCap
    }
```

Access becomes slice-friendly:

```fsharp
let! logger = capability _.Logging.Logger
let! clock  = capability _.Clock.Clock
let! device = capability _.DeviceOps
```

Or with helpers:

```fsharp
do! log Info "Polling"
let! now = utcNow
```

Those helpers are just constrained accessors over the runtime capability shape, not magic baked into one global runtime.

So I’d revise the model to:

```text
CoreRuntime     = universal mechanics: cancellation, annotations, service provider bridge
Capabilities    = typed operational slices: logging, metrics, tracing, clock, custom user caps
Env/Services    = business dependencies, record or DI
Policies        = block decorators over flows
```

That keeps your ZIO-style ergonomics while avoiding a closed world. The current plan is good; the main change is: **do not make RuntimeContext the extensibility point. Make typed capability slices the extensibility point, and let RuntimeContext/CoreRuntime stay tiny.**
