---
weight: 10
title: Your First Application
description: Run a root Flow with application-owned cancellation and scope cleanup.
type: docs
---


Use `App.run` when a Flow is the root of an application. It carries cancellation from the calling Async and returns
after the root scope and its resources have closed.

```fsharp
open Axial.Flow

let application : Flow<string> =
    flow {
        return "Hello from Flow"
    }

let run () =
    async {
        let! exit = App.run () application

        match exit with
        | Exit.Success message -> printfn "%s" message
        | Exit.Failure cause -> printfn "%s" (Cause.prettyPrint string cause)
    }
```

The `()` value is the environment because this workflow has no dependencies. An application with an `AppEnv` record
passes its live value in the same position.

Use `App.start` when a signal handler, desktop window, UI component, or external host will request shutdown later.

The [Application Lifecycle]({{< relref "/flow/applications/" >}}) guide covers start and stop ownership. Hosting
guides show the .NET, Node, and browser boundaries.

You now have the complete introductory path: describe work, compose it, distinguish outcomes, provide dependencies,
and run one root workflow.
