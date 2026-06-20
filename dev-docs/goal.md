# Goal: Replace Public Effect Surface With Explicit Execution Boundaries

## Objective

Axial should expose one public workflow model:

```fsharp
Flow<'env, 'error, 'value>
```

Users should not need to learn a second public concept named `Effect`, and shared libraries should not need different
workflow types for .NET, Fable, JavaScript, Python, or other Fable targets. Platform-specific execution carriers belong
only at the edges:

- when a `Flow` is started
- when a platform API is adapted into a `Flow` or `Layer`
- when a host/container boundary such as `IServiceProvider` builds the environment

The public model should read as:

- `Flow` is the cold workflow value.
- `Exit` is the completed workflow outcome.
- `Cause` explains typed failures, defects, and interruption.
- `Layer` provisions services.
- `Scope` manages resource lifetime.
- `ToTask`, `ToValueTask`, `ToAsync`, and `RunSynchronously` are execution boundaries.

## Motivation

The current `Effect` terminology leaks runtime plumbing into the public API.

Problems:

- `Effect<'value, 'error>` is not a separate Axial domain concept; it is the platform-specific execution carrier.
- `EffectFlow` sounds like a second workflow abstraction, but it is implementation machinery.
- User-facing docs mention "effect" in ways that make Axial feel like it has a hidden extra concept.
- Fable and .NET really do differ at the carrier level, but that difference should not infect ordinary composition or
  service package APIs.
- Service packages need one stable `Flow<'env, 'error, 'value>` shape so they can be consumed from .NET-only,
  Fable-only, and shared code without carrier noise.

The fix is not to split the whole library into carrier-specific workflow types. The fix is to keep composition
carrier-free and make execution/construction boundaries explicit.

## Non-Goals

- Do not introduce `Flow<'carrier, 'env, 'error, 'value>`.
- Do not return to separate public `AsyncFlow`, `TaskFlow`, `ValueTaskFlow`, or `PromiseFlow` types.
- Do not keep compatibility aliases. Axial is pre-1.0.
- Do not add ambient or global runtime execution.
- Do not make service packages carrier-specific.
- Do not add `ToPromise` / `fromPromise` in this goal unless required to remove existing public `Effect` usage.
- Do not spend excessive time polishing user-facing prose beyond making the current story coherent and non-stale.

## Core Decisions

- Keep `Flow<'env, 'error, 'value>` as the only public workflow type.
- Remove public `Effect<'value, 'error>`.
- Remove public `EffectFlow`.
- Remove public `Layer.effect`.
- Keep the platform carrier abstraction internal.
- Keep module-level `Flow` APIs for construction and composition.
- Move workflow execution to instance members on `Flow<'env, 'error, 'value>`.
- Use `fromTask`, `fromValueTask`, and `fromAsync` for adapting raw platform APIs into Axial.
- Use `ToTask`, `ToValueTask`, `ToAsync`, and `RunSynchronously` for starting a `Flow`.
- Use visible documentation metadata for platform-specific APIs.

## Public API Shape

### Flow Construction And Composition

Keep module-level APIs for building and composing workflows:

```fsharp
Flow.succeed
Flow.ok
Flow.fail
Flow.ofExit
Flow.fromResult
Flow.fromOption
Flow.fromValueOption
Flow.fromTask
Flow.fromValueTask
Flow.fromAsync
Flow.map
Flow.bind
Flow.zip
Flow.zipPar
Flow.race
Flow.provide
```

Composition remains module-first:

```fsharp
let workflow =
    Flow.succeed 1
    |> Flow.map ((+) 41)
```

### Flow Execution

Delete module-level execution functions:

```fsharp
Flow.run
Flow.runFull
Flow.runWithToken
Flow.toResult
Flow.toTaskResult
Flow.toAsyncResult
Flow.toValueTaskResult
```

Add execution as instance members on `Flow<'env, 'error, 'value>`.

.NET:

```fsharp
workflow.ToValueTask(env)
workflow.ToValueTask(env, cancellationToken = ct)

workflow.ToTask(env)
workflow.ToTask(env, cancellationToken = ct)

workflow.ToAsync(env)
workflow.ToAsync(env, cancellationToken = ct)

workflow.RunSynchronously(env)
workflow.RunSynchronously(env, timeout = 1000)
workflow.RunSynchronously(env, cancellationToken = ct)
workflow.RunSynchronously(env, timeout = 1000, cancellationToken = ct)
```

Fable:

```fsharp
workflow.ToAsync(env)
workflow.ToAsync(env, cancellationToken = ct)
```

Return types:

```fsharp
ToValueTask      : ValueTask<Exit<'value, 'error>>
ToTask           : Task<Exit<'value, 'error>>
ToAsync          : Async<Exit<'value, 'error>>
RunSynchronously : Exit<'value, 'error>
```

Use explicit `Exit` interpretation:

```fsharp
task {
    let! exit = workflow.ToTask(env)
    return Exit.toResult exit
}
```

Do not reintroduce result-returning execution helpers. Execution returns `Exit`; callers decide whether to preserve or
discard structured failure information.

### Execution Semantics

- `Flow` values are cold until an execution member is called.
- `ToValueTask`, `ToTask`, and `ToAsync` start the workflow and return a platform handle.
- `ToValueTask`, `ToTask`, and `ToAsync` do not synchronously wait for completion.
- Awaiting or running the returned platform handle observes `Exit<'value, 'error>`.
- `RunSynchronously` starts the workflow and blocks until an `Exit<'value, 'error>` is available.
- If no cancellation token is supplied to .NET `ToTask`, `ToValueTask`, or `RunSynchronously`, use
  `CancellationToken.None`.
- Do not introduce a global/default Axial cancellation token source.

`ToAsync` should align with FSharp.Core expectations:

```fsharp
workflow.ToAsync(env)
workflow.ToAsync(env, cancellationToken = ct)
```

- `workflow.ToAsync(env)` observes `Async.CancellationToken` when the async computation is started.
- `workflow.ToAsync(env, cancellationToken = ct)` uses the supplied token instead.
- This keeps normal F# piping possible:

```fsharp
workflow.ToAsync(env)
|> Async.RunSynchronously
```

### Layer Construction And Composition

Delete public:

```fsharp
Layer.effect
```

Add public:

```fsharp
Layer.fromAsync
Layer.fromTask
Layer.fromValueTask
```

Keep the layer redesign terminology:

```fsharp
Layer.succeed
Layer.fail
Layer.scoped
Layer.acquireRelease
Layer.merge
Layer.mergeAll
Layer.zip
Layer.zipPar
Layer.provide
```

Use these meanings consistently:

- `Layer.succeed`: provide an already-built service value.
- `Layer.fromAsync` / `Layer.fromTask` / `Layer.fromValueTask`: adapt platform construction into a layer.
- `Layer.scoped` / `Layer.acquireRelease`: build managed services whose lifetime is tied to `Scope`.
- `Layer.merge` / `Layer.mergeAll`: combine independent layers into a wider environment.
- `Layer.zip` / `Layer.zipPar`: combine layer outputs when tuple-shaped composition is desired.

Do not call these "effects" in docs.

### Hosting And IServiceProvider

Prefer explicit environment construction followed by instance execution:

```fsharp
let env = BaseRuntime.fromServiceProvider sp

workflow.ToValueTask(env)
workflow.ToTask(env)
workflow.RunSynchronously(env)
```

Remove hosting APIs that return public `Effect`.

Do not add duplicate hosting execution APIs such as:

```fsharp
Hosting.toValueTask
Hosting.toTask
Hosting.toAsync
Hosting.runSynchronously
```

The hosting package adapts host/container-backed construction into Axial. It should not become a second workflow
execution surface.

## Internal Runtime Shape

Use an internal carrier alias and helper module:

```fsharp
#if FABLE_COMPILER
type internal Execution<'value, 'error> = Async<Exit<'value, 'error>>
#else
type internal Execution<'value, 'error> = ValueTask<Exit<'value, 'error>>
#endif

module internal Execution
```

`Execution` is internal runtime plumbing. It must not appear in generated public reference docs.

The internal workflow representation should be:

```fsharp
type Flow<'env, 'error, 'value> =
    internal | Flow of ('env -> CancellationToken -> Execution<'value, 'error>)
```

Rename internal helpers:

```text
EffectFlow.ofValue          -> Execution.ofValue
EffectFlow.ofError          -> Execution.ofError
EffectFlow.ofExit           -> Execution.ofExit
EffectFlow.ofCause          -> Execution.ofCause
EffectFlow.ofDie            -> Execution.ofDie
EffectFlow.ofException      -> Execution.ofException
EffectFlow.ofInterrupt      -> Execution.ofInterrupt
EffectFlow.ofResult         -> Execution.ofResult
EffectFlow.fold             -> Execution.fold
EffectFlow.map              -> Execution.map
EffectFlow.bind             -> Execution.bind
EffectFlow.mapError         -> Execution.mapError
EffectFlow.mapBoth          -> Execution.mapBoth
EffectFlow.causeOfException -> Execution.causeOfException
```

If F# name resolution makes `type Execution` plus `module Execution` awkward, prefer
`[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]` on the module rather than exposing a
different public name.

If an internal layer constructor needs direct access to the carrier, keep it internal:

```fsharp
Layer.fromExecution
```

## Platform Matrix

Generated reference docs must clearly label platform-specific APIs.

Allowed labels:

- `.NET only`
- `Fable compatible`
- `Cross-platform`

Required labels:

- `.NET only`: `Flow.fromTask`, `Flow.fromValueTask`, `Layer.fromTask`, `Layer.fromValueTask`, `ToTask`,
  `ToValueTask`, `RunSynchronously`.
- `Fable compatible`: `Flow.fromAsync`, `Layer.fromAsync`, `ToAsync`.
- `Cross-platform`: normal carrier-free construction and composition APIs.

Avoid `Fable only` unless an API truly exists only for Fable, such as a future `ToPromise`.

Preferred implementation is source XML documentation metadata consumed by the doc generator:

```xml
<platforms>.NET only</platforms>
<platforms>Fable compatible</platforms>
<platforms>Cross-platform</platforms>
```

If custom XML tags are awkward in the first implementation, use consistent XML remarks and update docgen to render the
platform line prominently. Do not leave platform support implicit.

## Documentation Requirements

Terminology:

- Use `workflow` for a `Flow<'env, 'error, 'value>` value.
- Use `execution` for starting a workflow.
- Use `platform handle` for the returned `Task`, `ValueTask`, or `Async`.
- Use `Exit` for completed workflow outcome.
- Use `Cause` for structured failure reason.
- Use `service` for dependency contracts such as `IFileSystem`, `IHttp`, and `IConsole`.
- Use `service implementation` for concrete live/fake objects.
- Use `layer` for the provisioning mechanism that builds services.
- Use `provider` only for host/container-backed construction, especially `IServiceProvider` edges.

Avoid:

- `Effect` as an Axial user concept.
- `EffectFlow`.
- `Layer.effect`.
- `Flow.run`.
- `capability` for service packages.
- `runtime capability` for services.

Docs must explicitly teach:

```text
Flow is cold. ToValueTask, ToTask, and ToAsync start execution and return a platform handle. Await or run that handle
to observe the Exit. RunSynchronously starts execution and blocks until the Exit is available.
```

Remove generated/public effect docs:

```text
docs/reference/effect/**
site/content/reference/effect/**
```

Required user-facing updates:

- Getting started guide executes workflows with instance members.
- Core model / semantics docs describe execution boundaries instead of `Effect`.
- Layer docs use `Layer.from*`, `Layer.scoped`, and `Layer.acquireRelease`, not `Layer.effect`.
- Resource docs distinguish `use` / `use!` from `Scope` / `Layer.scoped`.
- Reference generation omits internal `Execution`.
- Platform-specific APIs are labelled in generated reference pages.

Do not hand-edit generated reference pages as the primary source of truth. Update source comments and doc generator
inputs first, then regenerate.

## API Shape Tests

Remove public expectations for:

```fsharp
Effect
EffectFlow
Flow.run
Flow.runFull
Flow.runWithToken
Flow.toResult
Flow.toTaskResult
Flow.toAsyncResult
Flow.toValueTaskResult
Layer.effect
Hosting.run
```

Add expectations for:

```fsharp
Flow<'env, 'error, 'value>.ToValueTask
Flow<'env, 'error, 'value>.ToTask
Flow<'env, 'error, 'value>.ToAsync
Flow<'env, 'error, 'value>.RunSynchronously
Flow.fromTask
Flow.fromValueTask
Flow.fromAsync
Layer.fromTask
Layer.fromValueTask
Layer.fromAsync
```

Make platform-specific assertions target-aware where needed.

## Implementation Notes

- Public APIs must not expose internal `Execution` in signatures.
- Internal modules returning `Execution` must be `internal`.
- Fable-compatible code must not reference `Task`, `ValueTask`, or `RunSynchronously`.
- .NET-only APIs must be guarded with `#if !FABLE_COMPILER`.
- Existing service packages should continue exposing ordinary `Flow<'env, 'error, 'value>` APIs.
- Service package docs and APIs should use `service` terminology, not `capability`.
- Source comments should avoid introducing `Effect` as a public concept.
- Tests should use public carrier constructors where possible.
- Internal tests may use internal helpers only when behavior cannot be expressed through public API.

## Acceptance

- `Flow<'env, 'error, 'value>` remains the single public workflow type used by services and examples.
- No public `Effect<'value, 'error>` type appears in generated API reference.
- No public `EffectFlow` module appears in generated API reference.
- No user-facing guide introduces `Effect` as an Axial concept.
- No public `Layer.effect` appears in generated API reference or guides.
- Module-level `Flow.run`, `Flow.runFull`, and `Flow.runWithToken` are gone.
- Result-returning execution helpers are gone.
- Flow execution examples use instance members:

```fsharp
workflow.ToTask(env)
workflow.ToValueTask(env)
workflow.ToAsync(env)
workflow.RunSynchronously(env)
```

- `RunSynchronously` returns `Exit<'value, 'error>`.
- `ToTask`, `ToValueTask`, and `ToAsync` are documented as starting execution but not synchronously waiting for completion.
- Public raw interop APIs use `fromTask`, `fromValueTask`, and `fromAsync`.
- Platform-specific APIs are clearly marked in generated reference docs.
- Internal `Execution` does not appear in generated public reference docs.
- Existing service packages still expose ordinary `Flow<'env, 'error, 'value>` helper APIs without carrier noise.
- Fable JS surface check passes without leaking .NET-only execution APIs.

## Validation

Run:

```text
bash scripts/check-source-inventory.sh
dotnet build Axial.slnx --nologo -v minimal
timeout 300s dotnet test tests/Axial.Tests/Axial.Tests.fsproj --no-build --nologo -v minimal
bash scripts/check-fable-js-surface.sh
bash scripts/generate-api-docs.sh
npm run build
timeout 180s bash scripts/preview-docs.sh
```

`npm run build` must be run from:

```text
site
```

Clean generated rendered output before committing unless those files are intentionally tracked changes:

```text
git restore site/public
git clean -fd site/public
```

Commit when complete.
