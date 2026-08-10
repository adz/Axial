---
title: Layers
description: Reusable provisioning for environments that need flow capabilities to build.
---

A layer builds an environment, and building it may itself need flow capabilities — awaiting a
connection, reading configuration, failing with a typed startup error, or acquiring something that
must be released again. `Axial.Layers` is a separate package because most applications never need
that.

**Start with a record.** Construct the environment directly and hand it to the workflow:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let env = { Clock = Clock.live; Log = Log.live; FileSystem = FileSystem.live }
let exit = workflow |> Flow.run env
```

That covers most applications, needs no package beyond the services themselves, and is what
[dependencies](/dependencies/index.html) documents.

Reach for a layer when construction is itself effectful:

- provisioning can fail, and the failure should be a typed startup error rather than an exception
- a service must be acquired and released, and its lifetime is the runtime's
- independent parts of the environment should be built in parallel
- a service needs another service in order to be constructed

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
open Axial.Layers

let runtime : Layer<unit, Never, AppEnv> =
    Layer.merge clockLayer connectionLayer
    |> Layer.map (fun (clock, connection) -> { Clock = clock; Connection = connection })

let program : Flow<unit, AppError, unit> = Layer.provide runtime workflow
```

`Layer.provide` is the boundary: it opens a scope, builds the layer inside it, runs the downstream
flow with the result, and closes the scope afterwards whether the flow succeeds, fails, or is
interrupted.

## Two real examples

Axial's own packages contain both of the cases that justify a layer.

**Provisioning that can fail with a typed error.** `Axial.PlatformService` builds the five standard services from a
host container:

```fsharp no-check reason="Excerpted from the Axial.PlatformService source"
let servicesFromServiceProvider
    : Layer<IServiceProvider, BaseRuntimeError, IClock * ILog * IRandom * IGuid * IEnvironmentVariables> =
    Layer.fromValueTask (fun (provider, _) _ ->
        task {
            match tryService<IClock> provider, tryService<ILog> provider, tryService<IRandom> provider,
                  tryService<IGuid> provider, tryService<IEnvironmentVariables> provider with
            | Ok clock, Ok log, Ok random, Ok guid, Ok environmentVariables ->
                return Exit.Success(clock, log, random, guid, environmentVariables)
            | Error name, _, _, _, _ | _, Error name, _, _, _ | _, _, Error name, _, _
            | _, _, _, Error name, _ | _, _, _, _, Error name ->
                return Exit.Failure(Cause.Fail(BaseRuntimeError.MissingService name))
        })
```

Read the error channel: `BaseRuntimeError`, not `Never`. **Construction itself can fail**, and it fails with a typed
error naming the missing service. A record cannot express that — you would throw, or return an option and push the
problem onto every caller. `Layer.provide` surfaces it as a typed startup failure before any workflow runs.

**A service built from another service.** `Axial.Hosting` turns what the host container has into what workflows
need:

```fsharp no-check reason="Excerpted from the Axial.Hosting source"
let layer (categoryName: string) : Layer<ILoggerFactory, Never, ILog> =
    Layer.fromValueTask (fun (loggerFactory, _) _ ->
        ValueTask<Exit<ILog, Never>>(Exit.Success(fromFactory categoryName loggerFactory)))
```

The type says it: consumes an `ILoggerFactory`, produces an `ILog`. The factory does not exist until the host starts,
so there is no record field to put it in — the layer is the conversion.

Compare with the case that does **not** need a layer. Wrapping a value that is already built and cannot fail is
`Layer.succeed`, which provisions nothing:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
Layer.succeed Console.live
```

Console, FileSystem, HttpClient and Process are all of this shape, which is why none of them depends on this package.

## Scopes are not part of this package

Scopes and `acquireRelease` are core, and work without layers. See
[scopes and resources](/dependencies/scopes-and-resources.html).

## In this section

1. [Layers](layers.html) — construction, composition, and provisioning failure.
2. [Tutorial](tutorial.html) — the same material worked end to end.
