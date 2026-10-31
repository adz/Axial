---
title: Your First Application
description: Run a root Flow with application-owned cancellation and scope cleanup.
---

# Your First Application

Use `App.run` when a Flow is the root of an application. It carries cancellation from the calling Async and returns
after the root scope and its resources have closed.

```fsharp prepare
// Setup for the checked example on this page.
open System
open Axial

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open Axial

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

```fsharp run
App.run () application |> Async.RunSynchronously |> shouldEqual (Exit.Success "Hello from Flow" : Exit<string, Never>)
```

`run` is the entry point: call `run () |> Async.RunSynchronously` from a `[<EntryPoint>]`, or hand the same `Async`
to an ASP.NET Core or Node host. The `()` value is the environment because this workflow has no dependencies. An
application with an `AppEnv` record passes its live value in the same position.

Use `App.start` when a signal handler, desktop window, UI component, or external host will request shutdown later.

The [Application Lifecycle](/platforms-and-hosting/application-lifecycle.html) guide covers start and stop ownership. Hosting
guides show the .NET, Node, and browser boundaries.

## Related guides

- [Application Lifecycle](/platforms-and-hosting/application-lifecycle.html) covers `App.start`, coordinated stop, and lifecycle ownership.
- [Scopes and Resources](/scopes/index.html) explains finalizers and
  acquisition/release safety.
- [Hosting](/hosting/dotnet.html) connects the root application to .NET, Node, or browser hosts.
