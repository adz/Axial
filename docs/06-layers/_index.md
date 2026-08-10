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

Scopes and `acquireRelease` are **not** part of this package — they are core, and work without
layers. See [scopes and resources](/dependencies/scopes-and-resources.html).

## In this section

1. [Layers](layers.html) — construction, composition, and provisioning failure.
2. [Tutorial](tutorial.html) — the same material worked end to end.
