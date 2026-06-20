# New Capability Approach

Status: research context. The live source of truth is `../PLAN.md`.

> Superseded research note: this file is still useful for explicit-capability thinking, but its `RuntimeContext` and
> ambient-core ideas are no longer the target v1 architecture.

This document is the closest research ancestor of the current direction after the research in `CAPS_SUMMARY.md` and
the older structural-accessor plan. Its central idea still applies: do not make user domain dependencies the main
capability story. Some examples below still mention `RuntimeContext<'runtime, 'context, 'env>` or capability-family
requirements in `'env`; those are research sketches, not the implemented public model.

Short version:

```text
Do not make user domain dependencies the main capability story.
Make Axial's capability story about explicit, typed, testable .NET/system effects.
Keep user dependencies boring by default: records, RuntimeContext env, or IServiceProvider.
In the implemented model, runtime/system services are ambient and do not appear in end-user `'env` signatures.
Offer fine-grained caps primarily through opt-in cap packages.
```

## Coherent Summary

Axial should have one workflow model and many optional capability families.

The researched workflow model was:

```text
TaskFlow<'env, 'error, 'value>
RuntimeContext<'runtime, 'context, 'env>  // proposed richer split
```

or, if keeping the current two-parameter shape:

```text
RuntimeContext<'runtime, AppEnv<'context, 'domainEnv>>
```

The implemented model keeps:

```text
Flow<'env, 'error, 'value>
```

with runtime/system services carried by the ambient runtime.

The conceptual split is:

```text
RuntimeCaps
  How the workflow runs.
  Clock, random, guid, console, file system, network, process, logging, tracing, metrics,
  IServiceProvider integration.

ContextCaps
  Who/what this execution is about.
  Request id, correlation id, tenant id, current user, locale, request metadata.

UserEnv / DomainEnv
  What business services this workflow needs.
  Orders, email, billing, inventory, repositories, gateways, device clients.
```

The biggest shift is that **domain dependencies are not the default capability story**.

Domain deps can still be scoped carefully:

```fsharp
type SubmitOrderEnv =
    { Orders : IOrderRepository
      Email : IEmailSender
      Inventory : IInventoryClient }
```

But Axial should not force every repository or domain service into an `IHasX` capability model.

Instead, Axial's strongest capability story is:

```text
Capify the ambient .NET/BCL/System.* effects.
Make them explicit in workflow types.
Give them typed errors.
Make them testable and swappable.
Package them as opt-in NuGets.
```

Examples:

```fsharp
open Axial.Capabilities.Core
open Axial.Capabilities.FileSystem
open Axial.Capabilities.Console

let readAndPrint path =
    taskFlow {
        let! now = Clock.utcNow
        let! text = File.readAllText path
        do! Console.writeLine $"[{now}] {text}"
        return text.Length
    }
```

That is a real capability story:

```text
this function needs clock + file + console
those effects are explicit
common failures are typed
tests can provide fake caps
runtime provisioning can be grouped
```

## Why The Earlier Direction Was Unsatisfying

Earlier documents explored:

- boilerplate records and slices
- `IServiceProvider`
- simple record SRTP
- structural accessors
- structural accessors plus DI bridge
- explicit interface/record hybrids
- leveled recommendations around records, providers, and nominal helpers

Those were useful, but the center of gravity was wrong.

If "capabilities" mostly means user app dependencies, it often feels like expensive argument passing:

```text
IHasOrders + OrderApp + Orders.repository()
```

instead of:

```fsharp
type SubmitOrderEnv =
    { Orders : IOrderRepository }
```

That boilerplate is only worth it for heavily reused helpers or shared libraries. It is not a good universal
recommendation.

The better center is:

```text
Axial capifies effects the platform currently hides.
User domain dependencies remain plain unless the user wants more precision.
```

## Core Principle

```text
Open controls vocabulary.
Provisioning controls availability.
NuGet reference controls whether the concept exists.
```

For example:

```fsharp
open Axial.Capabilities.FileSystem
```

brings `File.readAllText`, `Directory.enumerateFiles`, etc. into scope.

It does not magically provide a file system. The workflow must still run with a runtime that satisfies the file
system requirements.

If the user does not reference `Axial.Capabilities.FileSystem`, that API and dependency model do not exist in their world.

## Capability Families

Capability families are optional modules/packages. Each family exposes:

```text
1. operations
2. fine-grained slot contracts
3. live implementations
4. test/fake implementations where useful
5. provisioning helpers/layers
6. typed error models
```

### Core Caps

Package:

```text
Axial.Capabilities.Core
```

Dependencies:

```text
BCL only
```

Effects:

```text
clock
random
guid/new id
environment variables
basic cancellation helpers if not already in TaskFlow.Runtime
```

Example operations:

```fsharp
Clock.utcNow
Clock.sleep
Random.int
Random.bytes
Guid.newGuid
Environment.getVariable
```

Fine-grained contracts:

```fsharp
type IHasClock =
    abstract Clock : IClock

type IHasRandom =
    abstract Random : IRandom

type IHasGuid =
    abstract Guid : IGuid

type IHasEnvironment =
    abstract Environment : IEnvironment
```

Provisioning can be grouped:

```fsharp
type CoreRuntime =
    { ClockValue : IClock
      RandomValue : IRandom
      GuidValue : IGuid
      EnvironmentValue : IEnvironment }

    interface IHasClock with
        member x.Clock = x.ClockValue

    interface IHasRandom with
        member x.Random = x.RandomValue

    interface IHasGuid with
        member x.Guid = x.GuidValue

    interface IHasEnvironment with
        member x.Environment = x.EnvironmentValue
```

Design rule:

```text
Operations require fine-grained interfaces.
Provisioning can be grouped.
```

### Context Caps

Package:

```text
Axial.Capabilities.Context
```

Purpose:

```text
execution-scoped facts, not runtime mechanics and not domain services
```

Effects/facts:

```text
request id
correlation id
tenant id
current user
locale/culture
request metadata
feature flags if they are request-scoped
```

Example:

```fsharp
open Axial.Capabilities.Context

taskFlow {
    let! user = CurrentUser.get
    let! requestId = RequestId.get
    return user.Id, requestId
}
```

This should be separate from runtime and domain deps because request/user context often needs to flow into:

```text
logs
traces
auditing
authorization
business rules
```

but it should not come from DI or globals.

Potential shape:

```fsharp
type RequestContext =
    { RequestId : RequestId
      CorrelationId : CorrelationId
      User : CurrentUser option
      TenantId : TenantId option }
```

Fine-grained contracts:

```fsharp
type IHasRequestId =
    abstract RequestId : RequestId

type IHasCurrentUser =
    abstract CurrentUser : CurrentUser option
```

### Observability Caps

Packages:

```text
Axial.Capabilities.Observability
Axial.Capabilities.Observability.MicrosoftLogging
Axial.Capabilities.Observability.OpenTelemetry
```

Base package defines Axial-owned abstractions:

```text
LogEntry
Metric operations
Trace/span operations
annotations/baggage
```

Adapter packages fill those slots from provider libraries:

```text
MicrosoftLogging adapter -> ILogger / ILoggerFactory
OpenTelemetry adapter    -> ActivitySource / Meter / TracerProvider / MeterProvider
```

Core workflow code depends on Axial abstractions:

```fsharp
open Axial.Capabilities.Observability

taskFlow {
    do! Log.info "Submitting order"
    use! span = Trace.span "orders.submit"
    do! Metrics.increment "orders.submitted"
}
```

Host code chooses adapters:

```fsharp
open Axial.Capabilities.Observability.MicrosoftLogging
open Axial.Capabilities.Observability.OpenTelemetry

let runtime =
    Runtime.empty
    |> MicrosoftLogging.useLoggerFactory services
    |> OpenTelemetry.useTracerProvider tracerProvider
    |> OpenTelemetry.useMeterProvider meterProvider
```

This keeps `Axial` core dependency-light while giving rich host integration.

### Console Caps

Package:

```text
Axial.Capabilities.Console
```

Operations:

```fsharp
Console.write
Console.writeLine
Console.readLine
Console.readKey
```

Typed errors may be minimal because console operations often fail through IO or unsupported input/output.

Test implementation:

```text
captured output buffer
scripted input
```

### File System Caps

Package:

```text
Axial.Capabilities.FileSystem
```

Operations:

```fsharp
File.readAllText
File.writeAllText
File.exists
File.openRead
Directory.exists
Directory.create
Directory.enumerateFiles
Path.combine
Temp.createFile
```

Error model:

```fsharp
type FileError =
    | NotFound of path: string
    | Unauthorized of path: string
    | InvalidPath of path: string
    | AlreadyExists of path: string
    | IoError of path: string * message: string
    | Canceled
```

Operation shape:

```fsharp
File.readAllText :
  path: string ->
    TaskFlow<RuntimeContext<#IHasFileSystem, 'ctx, 'env>, FileError, string>
```

This is high-value because file APIs are:

```text
ambient
exception-heavy
hard to fake cleanly
inconsistent around cancellation
```

### Network / HTTP Caps

Packages:

```text
Axial.Capabilities.Http
Axial.Capabilities.Network
```

Start with HTTP before lower-level networking.

Operations:

```fsharp
Http.send
Http.getString
Http.getJson
Http.postJson
```

Runtime slot can be an `HttpClient` or an Axial-owned wrapper:

```fsharp
type IHttp =
    abstract Send : HttpRequestMessage * CancellationToken -> Task<HttpResponseMessage>
```

Error model:

```fsharp
type HttpError =
    | InvalidUri of string
    | Timeout
    | NetworkError of message: string
    | NonSuccessStatus of statusCode: int * body: string option
    | DecodeError of message: string
    | Canceled
```

Adapter package may support:

```text
IHttpClientFactory
named clients
resilience handlers
```

### Process Caps

Package:

```text
Axial.Capabilities.Process
```

Operations:

```fsharp
Process.run
Process.start
Process.capture
```

Error model:

```fsharp
type ProcessError =
    | FileNotFound of executable: string
    | StartFailed of message: string
    | NonZeroExit of code: int * stdout: string * stderr: string
    | Timeout
    | Canceled
```

This is also high-value because process APIs are exception-heavy and awkward to test.

### ServiceProvider Caps

Package:

```text
Axial.Capabilities.ServiceProvider
```

Dependency:

```text
Microsoft.Extensions.DependencyInjection.Abstractions
```

This package adds a new runtime slot:

```fsharp
type ServiceProviderSlot =
    { Services : IServiceProvider }

type IHasServiceProvider =
    abstract ServiceProvider : ServiceProviderSlot
```

Operations:

```fsharp
Service.get<'T>
Service.tryGet<'T>
Options.get<'T>
KeyedService.get<'T>
```

Shape:

```fsharp
Service.get<'T> :
  TaskFlow<RuntimeContext<#IHasServiceProvider, 'ctx, 'env>, MissingCapability, 'T>
```

This is not fully statically honest about every service registration, but it is host-native and useful.

If the package is not referenced, `Service.get<'T>` does not exist.

### Aspire / AppHost Caps

Package:

```text
Axial.Capabilities.Aspire
```

Dependency:

```text
Aspire / AppHost packages as needed
```

Purpose:

```text
make Axial runtime/caps easy to build from modern .NET app hosting
```

This should be an integration package, not core.

## Slot Model

There are two kinds of cap packages.

### Packages That Fill Existing Slots

These adapt provider libraries into Axial-owned slots.

Examples:

```text
Observability.MicrosoftLogging fills Log from ILogger.
Observability.OpenTelemetry fills Trace/Metrics from OpenTelemetry.
Http.AspNetCore fills Http from IHttpClientFactory.
```

### Packages That Add New Slots

These introduce a new capability family.

Examples:

```text
ServiceProviderCaps adds IServiceProvider access.
FileSystemCaps adds file/directory operations.
ProcessCaps adds process execution.
ConsoleCaps adds console IO.
```

Adding a statically checked slot means:

```text
1. define slot data
2. define IHasX contract
3. define operations requiring IHasX
4. provide live/test implementations
5. update the app runtime record or use provider-backed runtime
```

Example:

```fsharp
type ServiceProviderSlot =
    { Services : IServiceProvider }

type IHasServiceProvider =
    abstract ServiceProvider : ServiceProviderSlot

module Service =
    let get<'service>
        : TaskFlow<RuntimeContext<#IHasServiceProvider, 'ctx, 'env>, MissingCapability, 'service> =
        taskFlow {
            let! slot = Flow.readRuntime _.ServiceProvider

            match slot.Services.GetService typeof<'service> with
            | null -> return! TaskFlow.error { CapabilityType = typeof<'service> }
            | value -> return unbox<'service> value
        }
```

User runtime:

```fsharp
type AppRuntime =
    { CoreValue : CoreRuntime
      ServicesValue : ServiceProviderSlot }

    interface IHasClock with
        member x.Clock = x.CoreValue.Clock

    interface IHasServiceProvider with
        member x.ServiceProvider = x.ServicesValue
```

No generator means adding a statically checked slot requires editing the runtime record. That is the tradeoff.

## Fine-Grained Caps

The design should preserve fine-grained caps for Axial-provided effect families.

Rule:

```text
Operations require fine-grained interfaces.
Provisioning can be grouped.
```

Good:

```fsharp
Clock.utcNow requires IHasClock
Random.guid requires IHasRandom
Log.info requires IHasLog
File.readAllText requires IHasFileSystem
Service.get<'T> requires IHasServiceProvider
```

Provisioning can still be convenient:

```fsharp
type StandardRuntime =
    { ClockValue : IClock
      RandomValue : IRandom
      LogValue : LogEntry -> unit
      FileSystemValue : IFileSystem }

    interface IHasClock with
        member x.Clock = x.ClockValue

    interface IHasRandom with
        member x.Random = x.RandomValue

    interface IHasLog with
        member x.Log = x.LogValue

    interface IHasFileSystem with
        member x.FileSystem = x.FileSystemValue
```

This avoids losing the effect-level precision that made capabilities attractive.

For domain dependencies:

```text
IServiceProvider mode: fine-grained in code body, not in type.
Explicit record mode: fine-grained at the feature boundary.
Nominal IHasX mode: fine-grained in function types, but boilerplate-heavy.
```

The recommendation is:

```text
Keep fine-grained caps for Axial-provided runtime/system effects.
Let domain dependencies remain records or provider-backed by default.
Use nominal domain caps only for heavily reused helpers.
```

## Layers

Layers are not a separate capability model. They are provisioning/adaptation.

Current shape:

```fsharp
TaskFlow<'input, 'error, 'environment>
```

`provideLayer` turns:

```text
Layer:   input -> environment
Program: environment -> value
```

into:

```text
Program: input -> value
```

Use layers to build or adapt:

```text
IServiceProvider -> SubmitOrderEnv
IServiceProvider -> RuntimeContext<RuntimeCaps, ContextCaps, DomainEnv>
config -> CoreRuntime
request -> ContextCaps
test data -> fake FileSystem/Console/Http caps
```

Mapping to `docs/ARCHITECTURAL_STYLES.md`:

```text
Booted App Environment
  Layers build AppEnv from config, DI, secrets, connections, caches.
  Workflows read AppEnv.

Explicit Dependencies Plus Context
  Layers build the dependency record.
  The feature workflow can keep deps -> input -> Flow<'ctx,_,_>.

Standard .NET AppHost Plus DI
  DI is already a layer system.
  Axial layers/adapters convert IServiceProvider into runtime/context/env/caps.
```

## Package Strategy

Core package:

```text
Axial
  Result/Validation/Flow/AsyncFlow/TaskFlow
  RuntimeContext
  basic runtime helpers
  no heavy provider dependencies
```

Optional packages:

```text
Axial.Capabilities.Core
Axial.Capabilities.Context
Axial.Capabilities.Observability
Axial.Capabilities.Observability.MicrosoftLogging
Axial.Capabilities.Observability.OpenTelemetry
Axial.Capabilities.Console
Axial.Capabilities.FileSystem
Axial.Capabilities.Http
Axial.Capabilities.Process
Axial.Capabilities.ServiceProvider
Axial.Capabilities.Aspire
```

Reason:

```text
If a user does not reference a package, the concept does not exist for them.
No OpenTelemetry dependency unless they install the OpenTelemetry adapter.
No Microsoft.Extensions.DependencyInjection dependency unless they install ServiceProvider caps.
No AppHost/Aspire dependency unless they install Aspire caps.
```

This is good for:

```text
library size
dependency hygiene
documentation clarity
LLM coding
user onboarding
```

## LLM Coding Guidance

LLMs do best when the visible dependency shape is local and concrete.

Recommended order for agent-maintained code:

```text
1. explicit dependency records
2. named RuntimeContext records
3. provider-backed code close to host boundaries
4. nominal domain IHasX only when already established
```

However, Axial-provided cap families can be LLM-friendly because they are standardized:

```fsharp
Clock.utcNow
File.readAllText
Console.writeLine
Service.get<IThing>
```

The library owns those patterns, docs, errors, and tests. Agents can reuse them reliably.

Avoid asking agents to invent new domain capability structures unless examples already exist nearby.

## What Axial Provides If Domain Caps Stay Boring

Even without fine-grained domain caps, Axial still provides:

```text
typed failures
task/async/result orchestration
environment threading
explicit execution
runtime/context separation
retry/timeout/cancellation/resource helpers
layers/provideLayer
testable capified System.* effects
DI/AppHost interop through optional packages
```

The distinctive 1.0 story becomes:

```text
Axial makes effectful .NET code explicit, typed, composable, and testable.
```

not:

```text
Axial forces every app dependency into a capability framework.
```

## What To Do Next

1. Stop presenting domain `IHasX` as the main capability direction.
2. Keep explicit records and `IServiceProvider` as normal user dependency paths.
3. Design `RuntimeContext<'runtime, 'context, 'env>` or an equivalent nested context shape.
4. Define the first small cap family: `CoreCaps` with clock/random/guid/environment.
5. Define typed error conventions for cap packages.
6. Define package boundaries and dependency policy.
7. Prototype `ConsoleCaps` or `FileSystemCaps` because they prove testability and typed errors quickly.
8. Prototype `ServiceProviderCaps` because it proves AppHost ergonomics.
9. Update `CAPS_SUMMARY.md` after the prototype so it reflects cap families as the primary direction.
10. Keep the structural-accessor docs as historical research.
