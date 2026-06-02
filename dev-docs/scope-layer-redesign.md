# Scope, Services, and Layers Redesign

Status: proposed / active implementation target.
Recorded: 2026-06-02.

## Decision Summary

FsFlow should move to a single, explicit dependency story:

1. Delete the internal registry completely.
2. Keep `Flow<'env, 'error, 'value>` as the public workflow type.
3. Keep ambient runtime state only for closed executor mechanics.
4. Move former ambient operational services into explicit service capabilities.
5. Replace `Flow.service` and `Flow.inject` with `Service<'service>.get()` and `Service<'service>.resolve()`.
6. Make `Layer` the public provisioning abstraction.
7. Use layers to build a base runtime bundle for the former ambient services.

This is the v1 direction. Backward compatibility is not a design constraint.

---

## 1. Why This Change

The current codebase contains two competing stories:

- a clean public `Flow<'env, 'error, 'value>` model
- an internal registry-backed runtime foundation

That split is not coherent enough for v1.

Keeping the registry would preserve extensible ambient capabilities, but it would also keep a second dependency container
inside FsFlow alongside `IServiceProvider`. That creates hidden requirements, weakens type visibility, and makes the
mental model harder for both humans and LLMs:

- some dependencies are explicit in `'env`
- some dependencies are hidden in a type-indexed bag
- some provisioning happens through layers, some through ambient overrides

This redesign removes that split.

The new model is:

- explicit services for application and service dependencies
- a closed ambient executor runtime for execution mechanics only
- layers for provisioning and teardown
- `IServiceProvider` only at host boundaries

---

## 2. Goals

This redesign aims to produce a v1 architecture that is:

- coherent: one visible dependency model instead of parallel models
- explicit: important services remain visible in types
- testable: fake services can be supplied by records, nominal contracts, or layers
- composable: environments can be built and combined without hidden global state
- LLM-friendly: the access pattern should be obvious from the API spelling

---

## 3. Non-Goals

This redesign does not attempt to preserve:

- compatibility with the current `Flow.service` / `Flow.inject` surface
- the current ambient `Clock` / `Log` / `Random` / `Guid` / `EnvironmentVariable` access story
- the internal registry as a future extension hook

This redesign also does not make `IServiceProvider` the universal dependency model. It stays an edge input, not the
core architecture.

---

## 4. The New Dependency Taxonomy

FsFlow should teach and implement four distinct concepts.

| Concern | Mechanism | Visibility | Intended Use |
| --- | --- | --- | --- |
| Local app data or dependencies | `Flow.read` | explicit in `'env` | records and feature-local environments |
| Reusable named service contracts | `Service<'service>.get()` | explicit in `'env` via `IHas<'service>` | service modules and reusable helpers |
| Host container lookup | `Service<'service>.resolve()` | dynamic `IServiceProvider` boundary | glue code and app-host edges |
| Execution mechanics | closed ambient runtime | not part of `'env` | cancellation, scope, annotations, scheduling |

This is the key split:

- **Services are explicit**
- **execution mechanics are ambient**

The runtime is no longer a place to stash first-party or third-party capabilities.

---

## 5. What Stays Ambient

The ambient runtime should be reduced to a closed executor-owned set of mechanics:

- current `CancellationToken`
- current `Scope`
- runtime annotations and annotation sink
- interruption and scheduling helpers that are executor mechanics rather than business services

Examples of helpers that can remain on `Flow.Runtime`:

- `Flow.Runtime.cancellationToken`
- `Flow.Runtime.ensureNotCanceled`
- `Flow.Runtime.catchCancellation`
- `Flow.Runtime.sleep`
- `Flow.Runtime.annotations`
- `Flow.Runtime.traceId`
- `Flow.annotate`
- `Flow.traceId`

These are execution concerns, not service dependencies.

The following should no longer be ambient:

- `IClock`
- `ILog`
- `IRandom`
- `IGuid`
- `IEnvironmentVariables`

Those become ordinary explicit services.

---

## 6. What Gets Deleted

The following pieces should be removed from the public and internal architecture:

- internal `Registry`
- registry-backed service lookup as a concept
- `RuntimeAdapter`
- `Flow.service`
- `Flow.inject`
- `Flow.withClock`
- `Flow.withLog`
- `Flow.withRandom`
- `Flow.withGuid`
- `Flow.withEnvironmentVariables`
- ambient `Flow.Runtime.now`
- ambient `Flow.Runtime.log`
- ambient `Flow.Runtime.newGuid`
- ambient `Flow.Runtime.nextInt`
- ambient `Flow.Runtime.tryGetEnvironmentVariable`
- the current flow-to-flow `Flow.provideLayer`

If compatibility shims exist during migration, they should be treated as temporary implementation aids and not as part
of the target public story.

---

## 7. Service Access Model

### 7.1 `Flow.read`

`Flow.read` remains the default accessor for explicit environments.

Use it for:

- plain records
- request data
- feature-local dependencies
- environment projections that do not need a reusable named contract

```fsharp
type AppEnv =
    { Orders: IOrderRepository
      CurrentUser: CurrentUser }

let saveOrder order : Flow<AppEnv, OrderError, unit> =
    flow {
        let! orders = Flow.read _.Orders
        let! user = Flow.read _.CurrentUser
        return! orders.Save(user.UserId, order)
    }
```

### 7.2 `Service<'service>.get()`

`Service<'service>.get()` is the statically honest accessor.

```fsharp
type IHas<'service> =
    abstract Service : 'service

type Service<'service> =
    static member inline get<'env, 'error when 'env :> IHas<'service>> ()
        : Flow<'env, 'error, 'service> =
        Flow.read (fun (env: 'env) -> env.Service)
```

Use it for:

- first-party service modules
- reusable helpers that should advertise a named service requirement
- package APIs where the dependency contract should be obvious in the type

Example:

```fsharp
type IOrderPersistenceEnv =
    inherit IHas<IOrderRepository>
    inherit IHas<ILog>

let persist order : Flow<IOrderPersistenceEnv, OrderError, unit> =
    flow {
        let! orders = Service<IOrderRepository>.get()
        let! log = Service<ILog>.get()
        do! log.Info $"Saving order {order.Id}" |> Flow.ofTask
        return! orders.Save order
    }
```

### 7.3 `Service<'service>.resolve()`

`Service<'service>.resolve()` is the dynamic host-edge accessor.

```fsharp
type Service<'service> =
    static member inline resolve<'env, 'error when 'env :> IServiceProvider> ()
        : Flow<'env, 'error, 'service> =
        Flow.read (fun (env: 'env) ->
            let service = env.GetService(typeof<'service>)
            if isNull (box service) then
                failwith $"Service {typeof<'service>.Name} was not registered in the IServiceProvider."
            else
                unbox<'service> service)
```

Use it for:

- host glue
- controllers, handlers, startup glue, and prototypes
- thin adapters that have chosen dynamic host lookup on purpose

Missing registrations are configuration defects and should fail as defects, not typed business errors.

If callers want typed bootstrap validation, they should build an explicit environment with a `Layer` instead of calling
`Service.resolve()` deep in business logic.

### 7.4 Naming Rationale

The naming should carry the mental model directly:

- `read` means projection from explicit environment
- `get` means statically proven service access
- `resolve` means dynamic container resolution

This is clearer than the old `service` / `inject` split because `resolve` names the `IServiceProvider`-style operation
more honestly.

---

## 8. Former Ambient Services Become Explicit Service Packages

The former ambient core services should move to the same model as the service packages.

This means:

- `Clock.now` becomes a thin wrapper over `Service<IClock>.get()`
- `Log.info` becomes a thin wrapper over `Service<ILog>.get()`
- `Random.nextInt` becomes a thin wrapper over `Service<IRandom>.get()`
- `Guid.newGuid` becomes a thin wrapper over `Service<IGuid>.get()`
- `EnvironmentVariable.get` becomes a thin wrapper over `Service<IEnvironmentVariables>.get()`

The important point is that the nice helper modules can stay, but their dependency model changes from ambient runtime to
explicit service access.

Example:

```fsharp
[<RequireQualifiedAccess>]
module Clock =
    let now<'env, 'error when 'env :> IHas<IClock>> : Flow<'env, 'error, DateTimeOffset> =
        flow {
            let! clock = Service<IClock>.get()
            return clock.UtcNow()
        }
```

This same pattern should be used for:

- `FsFlow.Services.Core`
- `FsFlow.Services.Console`
- `FsFlow.Services.FileSystem`
- `FsFlow.Services.Http`
- `FsFlow.Services.Process`
- future `Network`
- telemetry-related service packages

The library should stop treating "core" services as a special dependency mechanism.

---

## 9. Scope

`Scope` should become the public resource-lifetime primitive for layers and advanced resource helpers.

### 9.1 Purpose

A scope owns finalizers for resources acquired during provisioning or runtime execution.

Examples:

- opened database connections
- started subprocesses
- `IDisposable` or `IAsyncDisposable` adapters
- telemetry or logging sinks that must be flushed

### 9.2 Required Behavior

The v1 scope contract should be:

1. Finalizers run in reverse registration order.
2. Finalizers run at most once.
3. Registering after closure fails.
4. Cleanup failures are aggregated.
5. Cleanup failures are defects, not typed business errors.
6. The executor owns the root scope for `Flow.provide`.

### 9.3 Finalizer Shape

`Scope` should support asynchronous finalizers.

The exact API can vary, but it should be equivalent in power to:

```fsharp
type Scope =
    member AddFinalizer : (CancellationToken -> Task) -> unit
    member AddDisposable : IDisposable -> unit
    member AddAsyncDisposable : IAsyncDisposable -> unit
```

Synchronous-only finalizers are too limiting for process cleanup, telemetry flush, and future richer service
packages.

### 9.4 What Scope Is Not

`Scope` is not:

- a service registry
- a dependency lookup container
- a substitute for `Layer`

It owns teardown only.

---

## 10. Layer

`Layer` should become the single public provisioning abstraction.

### 10.1 Purpose

A layer builds an output environment from an input environment and a scope:

```fsharp
type Layer<'input, 'error, 'output> =
    ('input * Scope) -> CancellationToken -> Effect<'output, 'error>
```

Conceptually:

- `Flow` runs business logic using an already available environment
- `Layer` constructs the environment that `Flow` needs

### 10.2 Required Minimal Surface

The public surface should stay small and obvious. The minimum useful operations are:

```fsharp
module Layer =
    val succeed : 'output -> Layer<'input, 'error, 'output>
    val read : ('input -> 'output) -> Layer<'input, 'error, 'output>
    val effect :
        (('input * Scope) -> CancellationToken -> Effect<'output, 'error>) ->
        Layer<'input, 'error, 'output>
    val map :
        ('output -> 'next) ->
        Layer<'input, 'error, 'output> ->
        Layer<'input, 'error, 'next>
    val bind :
        ('output -> Layer<'input, 'error, 'next>) ->
        Layer<'input, 'error, 'output> ->
        Layer<'input, 'error, 'next>
    val zip :
        Layer<'input, 'error, 'left> ->
        Layer<'input, 'error, 'right> ->
        Layer<'input, 'error, 'left * 'right>
```

The exact helper names can differ, but the core ideas should not.

### 10.3 `Flow.provide`

`Layer` should be applied to flows through a single public function:

```fsharp
module Flow =
    val provide :
        Layer<'input, 'error, 'env> ->
        Flow<'env, 'error, 'value> ->
        Flow<'input, 'error, 'value>
```

Semantics:

1. Create a fresh root scope.
2. Build the layer inside that scope.
3. Run the downstream flow with the built environment.
4. Close the scope after the downstream flow completes or fails.
5. If layer acquisition fails partway through, already-acquired resources are finalized before returning the failure.

This should replace the current flow-based `Flow.provideLayer`.

Pure environment remapping should continue to use `Flow.localEnv`, not `Layer`.

### 10.4 Composition Rules

The spec should treat these rules as required behavior:

#### Vertical composition

When one layer depends on the output of another:

```fsharp
let appLayer =
    configLayer
    |> Layer.bind dbLayer
```

The outer scope owns everything acquired by the composed layer.

#### Horizontal composition

When two layers are independent:

```fsharp
let combined =
    Layer.zip loggingLayer httpLayer
```

Both acquisitions belong to the same root provisioning scope for that combined layer. If one side fails, the other
side is finalized before the failure escapes.

### 10.5 What Layer Replaces

Layer replaces three previous roles:

1. building environments from `IServiceProvider`
2. building live service bundles for tests or applications
3. acquiring scoped resources before business flows run

It should not be treated as optional architecture garnish. It is the provisioning model.

---

## 11. Building a Base Runtime with Layers

The former ambient core services should have a recommended explicit bundle that can be built by layers.

### 11.1 Base Runtime Bundle

One reasonable default shape is:

```fsharp
type BaseRuntime =
    { Clock: IClock
      Log: ILog
      Random: IRandom
      Guid: IGuid
      EnvironmentVariables: IEnvironmentVariables }
    interface IHas<IClock> with member x.Service = x.Clock
    interface IHas<ILog> with member x.Service = x.Log
    interface IHas<IRandom> with member x.Service = x.Random
    interface IHas<IGuid> with member x.Service = x.Guid
    interface IHas<IEnvironmentVariables> with member x.Service = x.EnvironmentVariables
```

This bundle is not ambient runtime state. It is an ordinary explicit environment value that happens to group the
former core services.

### 11.2 Live Base Runtime Layer

FsFlow should ship a straightforward live layer for that bundle:

```fsharp
module BaseRuntime =
    let live : Layer<unit, Never, BaseRuntime> =
        Layer.succeed
            { Clock = Clock.live
              Log = Log.live
              Random = Random.live
              Guid = Guid.live
              EnvironmentVariables = EnvironmentVariables.live }
```

### 11.3 Provider-Backed Base Runtime Layer

There should also be a clear example of building the same bundle from `IServiceProvider`:

```fsharp
type StartupError =
    | MissingService of string

module BaseRuntime =
    let fromServiceProvider : Layer<IServiceProvider, StartupError, BaseRuntime> =
        Layer.effect (fun (sp, _) _ ->
            task {
                let get<'service> () =
                    match sp.GetService(typeof<'service>) with
                    | null -> Error (MissingService typeof<'service>.Name)
                    | service -> Ok (unbox<'service> service)

                match get<IClock>(), get<ILog>(), get<IRandom>(), get<IGuid>(), get<IEnvironmentVariables>() with
                | Ok clock, Ok log, Ok random, Ok guid, Ok envVars ->
                    return
                        Exit.Success
                            { Clock = clock
                              Log = log
                              Random = random
                              Guid = guid
                              EnvironmentVariables = envVars }
                | Error e, _, _, _, _
                | _, Error e, _, _, _
                | _, _, Error e, _, _
                | _, _, _, Error e, _
                | _, _, _, _, Error e ->
                    return Exit.Failure (Cause.Fail e)
            })
```

This is important because it shows the right boundary discipline:

- `Service.resolve()` is fine for direct dynamic access
- `Layer` is better when you want to validate and construct a reusable explicit environment up front

### 11.4 App Environment Composition

Application code should then compose this base runtime with its own services:

```fsharp
type AppEnv =
    { Runtime: BaseRuntime
      Orders: IOrderRepository
      Email: IEmailSender }
    interface IHas<IClock> with member x.Service = x.Runtime.Clock
    interface IHas<ILog> with member x.Service = x.Runtime.Log
    interface IHas<IRandom> with member x.Service = x.Runtime.Random
    interface IHas<IGuid> with member x.Service = x.Runtime.Guid
    interface IHas<IEnvironmentVariables> with member x.Service = x.Runtime.EnvironmentVariables
    interface IHas<IOrderRepository> with member x.Service = x.Orders
    interface IHas<IEmailSender> with member x.Service = x.Email
```

Then:

```fsharp
let appLayer : Layer<IServiceProvider, StartupError, AppEnv> =
    Layer.zip BaseRuntime.fromServiceProvider domainLayer
    |> Layer.map (fun (runtime, domain) ->
        { Runtime = runtime
          Orders = domain.Orders
          Email = domain.Email })
```

The base runtime is therefore no longer magical. It is just another explicit service bundle built by layers.

---

## 12. Testing and Local Overrides

Without ambient service overrides, tests should override dependencies explicitly.

That is a feature, not a regression.

Recommended approaches:

1. build a fake environment record directly
2. provide a fake layer instead of a live layer
3. use `Flow.localEnv` for pure environment remapping

Example:

```fsharp
let fakeRuntime =
    { Clock = Clock.fromValue fixedNow
      Log = TestLog messages
      Random = Random.fromValue 4
      Guid = Guid.fromValue fixedGuid
      EnvironmentVariables = EnvironmentVariables.fromPairs [ "MODE", "test" ] }
```

This keeps overrides visible in normal F# values instead of hiding them in executor state.

---

## 13. Service Package Guidance

This redesign should drive the first-party packages toward a consistent shape.

### 13.1 Package contract shape

Each service package should expose:

- one or more service interfaces
- helper modules that call `Service<'service>.get()`
- a live implementation
- one or more layers for constructing the live implementation

### 13.2 Package examples

#### Console

- `IConsole`
- `Console.readLine`
- `Console.writeLine`
- `Console.live`
- `Console.layer`

#### FileSystem

- `IFileSystem`
- `FileSystem.readAllText`
- `FileSystem.writeAllText`
- `FileSystem.live`
- `FileSystem.layer`

#### Http

- `IHttp`
- `Http.getString`
- `Http.send`
- `Http.live`
- `Http.layer`

#### Process

- `IProcess`
- `Process.execute`
- `Process.startScoped` or equivalent scoped helper when needed
- `Process.live`
- `Process.layer`

#### Telemetry

Telemetry should stop depending on an implicit ambient `ILog`/annotation mix for its main value proposition. It should
have explicit service contracts and optionally integrate with runtime annotations and current scope where executor
mechanics are genuinely involved.

---

## 14. Implementation Recommendations

The clean implementation order is:

1. Add `Service<'service>.get()` and `Service<'service>.resolve()`.
2. Migrate first-party service helpers to the new accessor type.
3. Introduce public `Scope` with async finalizers.
4. Introduce public `Layer`.
5. Replace the current flow-based `provideLayer` with `Flow.provide`.
6. Migrate former ambient core service modules to explicit services.
7. Delete `Flow.service`, `Flow.inject`, and ambient operational overrides.
8. Delete `Registry` and `RuntimeAdapter`.
9. Update examples and docs in the same implementation wave.

The important sequencing rule is:

- do not leave both the old and new stories presented as equally supported for long

Short migration scaffolding is acceptable during implementation, but the final public model should be singular.

---

## 15. Documentation Rollout

This redesign requires a full documentation rewrite, not a wording patch.

Public docs should present one clear golden path.

### 15.1 New narrative order

The recommended teaching order is:

1. `Flow<'env, 'error, 'value>` and explicit environments
2. `Flow.read`
3. `Service<'service>.get()`
4. `Service<'service>.resolve()`
5. `Layer`
6. `Scope`
7. building a base runtime from layers
8. service packages as explicit services

### 15.2 Active doc entry points to replace

The old dependency-model guides should be removed rather than left in place with transition notes.

The active entry points that must be rewritten or replaced are:

- `docs/managing-dependencies/_index.md`
- `docs/tutorials/_index.md`
- `llms.txt`
- `docs/AGENT.md`
- `docs/index.md`

### 15.3 Recommended new pages

The docs tree should add pages close to this shape:

- `docs/managing-dependencies/explicit-services.md`
- `docs/managing-dependencies/layers.md`
- `docs/managing-dependencies/scopes-and-resources.md`
- `docs/managing-dependencies/building-a-base-runtime.md`
- `docs/managing-dependencies/service-provider-boundaries.md`
- `docs/tutorials/layers.md`

### 15.4 Reference changes

The generated reference docs must reflect the new surface:

- remove reference pages for `Flow.service`
- remove reference pages for `Flow.inject`
- remove reference pages for ambient runtime helpers that disappear
- add reference pages for `Service<'service>`
- add reference pages for public `Layer`
- add reference pages for public `Scope`

### 15.5 Documentation message

The public message should be:

- FsFlow does not hide first-party capabilities in a magical ambient bag
- explicit services are the normal model
- layers provision services and own teardown
- `IServiceProvider` is an edge, not the center

That should be consistent across guides, examples, reference docs, and `llms.txt`.

---

## 16. Final Architecture Statement

The v1 architecture should be stated plainly:

- FsFlow has explicit services, not extensible ambient capabilities.
- The runtime is closed and executor-owned.
- Layers build environments.
- Scope owns cleanup.
- `Service.get()` is honest.
- `Service.resolve()` is pragmatic.
- `IServiceProvider` stays at the edge.

That is the coherent story to implement and document.
