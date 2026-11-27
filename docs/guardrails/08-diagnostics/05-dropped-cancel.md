---
title: "dropped-cancel (AXG005): pass the cancellation token on"
linkTitle: dropped-cancel
description: Reports task adapters that ignore the cancellation token Flow gives them.
---

# dropped-cancel (AXG005)

## The problem

When a flow is interrupted, by a timeout or a lost race, Axial cancels the token it gave each running task. A task
that never received the token keeps running. The flow reports a timeout while the HTTP request it started goes on to
the end.

```fsharp
open System.Net.Http

/// Reported: the request keeps running after a timeout.
let fetchIgnoringCancel (client: HttpClient) (url: string) =
    Flow.fromTask (fun _ -> client.GetStringAsync url)

/// Fixed: the timeout cancels the request.
let fetch (client: HttpClient) (url: string) =
    Flow.fromTask (fun ct -> client.GetStringAsync(url, ct))
```

## What it reports

A `fun _ -> …` lambda passed to `ColdTask`, `ColdTask.create`, `Flow.fromTask` or `Flow.fromTaskResult`.

It does not run in test projects by default, because test adapters often wrap completed tasks with nothing to cancel.
`AxialGuardrailsInTests=all` turns it on there.

## When it is deliberate

When the API has no overload that takes a token, add a reason:

```fsharp
/// An API with no cancellation support.
let legacyCall () : Task<int> = Task.FromResult 42

let legacy = ColdTask(fun _ -> legacyCall ()) // axial-allow-dropped-cancel: legacy API has no token overload
```
