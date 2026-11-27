---
title: "task-surface (AXG014): keep raw Task at the edge"
linkTitle: task-surface
description: Points out raw task code and Task-returning signatures in domain and service code. Information only; never fails a build.
---

# task-surface (AXG014)

## The problem

A function that returns `Task<string>` has already started. It cannot be retried, timed out or cancelled by the flow
that calls it, and a test cannot swap it out. Logic written in `task { }` inherits all of that. A flow is a
description: the caller decides when it runs, and timeouts, retries and interruption apply to all of it.

```fsharp
open System.Net.Http
open System.Threading.Tasks

/// Reported in domain code.
let fetchTitleTask (client: HttpClient) (url: string) : Task<string> =
    task {
        let! body = client.GetStringAsync url
        return body.Trim()
    }
```

```fsharp
open System.Net.Http
open Axial

/// Fixed: the logic is a flow; only the one .NET call is a task, with the flow's token.
let fetchTitle (client: HttpClient) (url: string) : Flow<unit, string, string> =
    flow {
        let! body = Flow.fromTask (fun ct -> client.GetStringAsync(url, ct))
        return body.Trim()
    }
```

In domain code, prefer a service such as `Axial.HttpClient.IHttp` over a raw client.

This rule is information only. It never fails a build, at any level or checkpoint. `axial check` prints each
project's count, so you can watch it go down.

## A bridge is one call

In capability and host files, a task that wraps one .NET call is fine. A task that composes several is logic that
belongs in a flow:

```fsharp
open System.Net.Http
open Axial

/// Reported in a capability: two calls sequenced inside one task.
let readBothInOneTask (client: HttpClient) (first: string) (second: string) : Flow<unit, string, string> =
    Flow.fromTask (fun ct ->
        task {
            let! a = client.GetStringAsync(first, ct)
            let! b = client.GetStringAsync(second, ct)
            return a + b
        })
```

```fsharp
open System.Net.Http
open Axial

let get (client: HttpClient) (url: string) : Flow<unit, string, string> =
    Flow.fromTask (fun ct -> client.GetStringAsync(url, ct))

/// Fixed: each bridge is one call, and the composition is a flow.
let readBoth (client: HttpClient) (first: string) (second: string) : Flow<unit, string, string> =
    flow {
        let! a = get client first
        let! b = get client second
        return a + b
    }
```

## What it reports

| File role | Reported |
| --- | --- |
| `domain`, `services` | Every `task { }` and `backgroundTask { }`, and every public function, value, member or abstract member returning `Task`, `Task<'T>`, `ValueTask` or `ValueTask<'T>` |
| `capabilities`, `host` | A task that composes: more than one `let!`/`do!`/`use!`/`match!`/`return!`, a loop, or an `if`/`match` that chooses what to await |
| A file claiming `concurrency` | Nothing |
| `test` | Nothing, unless `AxialGuardrailsInTests=all`; then the bridge rule |

Not counted as composing: a `try/with` or `match` on the one result, and an argument check before the await.
Overrides and interface implementations are not reported, because the interface fixes the signature. Private and
internal declarations are not public. A binding such as `let run () = task { ... }` is one finding, not two.

Full mode resolves builder types, so `let t = task` is caught and an unrelated value named `task` is not. Fast
matches names. In a file that opens `FSharp.Control.Tasks` (Ply, TaskBuilder.fs), only FSharp.Core's builders count.

## When it is deliberate

To hand a flow to a Task-based host, run it at the boundary with `flow.StartAsTask(env, ct)`. For an intentional
Task surface, add `// axial-note task-surface: <reason>`. The finding stays as acknowledged Info.
