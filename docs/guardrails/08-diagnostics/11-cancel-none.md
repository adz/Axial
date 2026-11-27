---
title: "cancel-none (AXG011): pass the owner's token"
linkTitle: cancel-none
description: Reports CancellationToken.None, which makes work that no timeout or shutdown can stop.
---

# cancel-none (AXG011)

## The problem

`CancellationToken.None` means "nothing can cancel this". A timeout fires, the app shuts down, and the work goes on.
It is usually written because a token was not at hand, not because the work must not stop.

```fsharp
open System.Threading
open System.Net.Http

/// Reported: no timeout or shutdown can stop this download.
let downloadUnstoppable (client: HttpClient) (url: string) =
    client.GetStringAsync(url, CancellationToken.None)

/// Fixed: the caller's token stops it.
let download (client: HttpClient) (url: string) (token: CancellationToken) =
    client.GetStringAsync(url, token)
```

Inside a flow, `Flow.fromTask (fun ct -> …)` gives you the token. Cleanup that must finish even after cancellation
belongs in a release action (`Flow.ensuring`, `Resource.create`), which Axial runs uninterruptibly.

## What it reports

Every `CancellationToken.None`, except in Axial's one `Cancellation.never` definition. It is an error, and no
allowance or note exempts it.

Test projects are exempt, unless `AxialGuardrailsInTests=all`. A host project can be listed under
[`[exemptions] cancel-none`](../policy-file.html#exempt-a-host-project-from-a-concurrency-rule).
