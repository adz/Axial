---
title: Browser Hosting
linkTitle: Browser
weight: 30
description: Owning a Fable Flow application from a browser UI module or AbortSignal.
---

# Browser Hosting

`Axial.Flow.Hosting.Browser` is a JavaScript-only Fable package. It connects a root `App` to browser ownership without
pretending the browser has a dependable process-shutdown phase.

```sh
dotnet add package Axial.Flow.Hosting.Browser
```

The package's .NET target asset exists for Fable project compilation. Calling its entry points outside Fable browser
JavaScript fails immediately.

## UI-Owned Mount

Mount the application when the UI root or feature is created:

```fsharp
open Axial.Flow.Hosting.Browser

let running =
    BrowserApp.mount browserEnvironment application
```

When the owner unmounts, request stop and allow cleanup to finish:

```fsharp
let dispose () = async {
    let! _ = running.Stop()
    return ()
}
```

React hooks, Elmish program ownership, custom elements, and other UI frameworks can call the same pair. The hosting
package does not depend on one framework.

## AbortSignal Ownership

Use `startWithSignal` when the owner already exposes an `AbortSignal`:

```fsharp
open Fable.Core.JsInterop

let controller: obj = emitJsExpr () "new AbortController()"
let signal: AbortSignal = emitJsExpr controller "$0.signal"

let running =
    BrowserApp.startWithSignal
        signal
        browserEnvironment
        application

// Later:
emitJsStatement controller "$0.abort()"
```

The structural `AbortSignal` binding avoids requiring a particular browser-binding package. An already-aborted signal
requests stop immediately. The event listener is removed after application completion.

## Page Lifecycle Is Not Application Shutdown

The adapter deliberately does not stop applications on `visibilitychange`, `pagehide`, or `beforeunload`:

- a hidden tab can become visible again;
- `pagehide` may place the page in the back/forward cache;
- browsers do not guarantee time for asynchronous unload cleanup.

The UI owner should stop work when its actual ownership ends. For data that must survive page termination, use the
browser's persistence or delivery mechanisms rather than relying on Flow finalizers during unload.

## Outcomes

Browsers have no process exit code. Observe `running.Completion` and translate the structured `Exit` into application
state, a fatal-error screen, or an error-reporting adapter:

```fsharp
async {
    let! exit = running.Completion

    match exit with
    | Exit.Success () -> ()
    | Exit.Failure Cause.Interrupt -> () // normal unmount
    | Exit.Failure cause -> showFatalError cause
}
|> Async.StartImmediate
```

Do not flatten typed failures, interruption, and defects into the same UI message before the application edge decides
how each should be handled.
