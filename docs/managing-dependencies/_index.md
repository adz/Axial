---
weight: 40
title: Managing Dependencies
description: How FsFlow models dependency boundaries with explicit environments and boundary-only host integration.
---

# Managing Dependencies

Keep dependencies explicit.

In FsFlow, the environment (`'env`) is the part of the world your workflow actually needs. The least surprising model is
also the preferred one:

- put application services and request-specific data in an explicit environment value
- read from that environment with `Flow.read`
- keep `IServiceProvider` at the outer boundary where the host already owns it

## Default Shape

Plain F# records are the default recommendation because they are legible, easy to fake in tests, and easy to refactor.

```fsharp
type ApiDeps = { Orders: IOrderRepo; Email: IEmailSender }

let workflow : Flow<ApiDeps, string, unit> =
    flow {
        let! email = Flow.read _.Email
        do! email.SendConfirmation()
    }
```

Keep the boundary concrete unless a named abstraction clearly pays for itself.

## Host Boundaries

`IServiceProvider` still belongs at the edge. Use it to construct the explicit environment that your core workflow
receives, rather than letting container lookup become the default model inside business logic.

## Tutorials

For a concrete starting point, use the [AppRecord tutorial](../tutorials/app-record/).
