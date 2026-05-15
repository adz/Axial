---
weight: 30
title: "HostContext"
description: Splitting host services from application dependencies.
---

# HostContext

`HostContext<'host, 'appEnv>` gives your app services a separate lane from the host.

This gives you:

- logging, metrics, tracing, or clocking belong to the host
- your app services belong to a feature module or boundary record
- the same host services should be shared across multiple areas
- the cancellation token should travel with the execution model

`HostContext` is the execution carrier above the adapter layer. It is not the host storage engine
and it is not the only way to model a dependency boundary.

## What Goes Where

- `Host` holds host services such as logging, metrics, tracing, and clocks.
- `AppEnv` holds your app services.
- `CancellationToken` belongs to the active run.

```fsharp
open FsFlow
open FsFlow.Capabilities.Core

type HostServices =
    { Log : LogEntry -> unit
      Clock : IClock }

type ApiDeps =
    { Orders : IOrderRepository
      Email : IEmailSender }

let host =
    { Log = printfn "%A"
      Clock = Clock.fromValue (DateTimeOffset.UtcNow) }

let apiDeps =
    { Orders = InMemoryOrders()
      Email = ConsoleEmail() }

let context =
    HostContext.create host apiDeps cancellationToken
```

## Reading The Split

`Flow.readHost` and `Flow.readAppEnv` read the two halves.
`Resolver.host` and `Resolver.appEnv` read the same split at the host edge.

```fsharp
let workflow : Flow<HostContext<HostServices, ApiDeps>, string, Guid> =
    flow {
        let! host = Flow.readHost id
        let! appEnv = Flow.readAppEnv id

        host.Log { Level = LogLevel.Information; Message = "starting"; TimestampUtc = host.Clock.UtcNow() }

        let! order = appEnv.Orders.Create()
        do! appEnv.Email.SendConfirmation order
        return order.Id
    }
```

Here, `host` is the host and `appEnv` is the app service set the workflow actually uses.

## What Works With HostContext

Works with any environment:

- `Flow.env`
- `Flow.read`
- `Flow.localEnv`
- `Flow.provideLayer`
- `Resolver.resolve`
- `Resolver.fromProvider`
- `Flow.readHost`
- `Flow.readAppEnv`

HostContext-specific:

- `HostContext.create`
- `HostContext.host`
- `HostContext.appEnv`
- `HostContext.cancellationToken`
- `Flow.readHost`
- `Flow.readAppEnv`
- `Resolver.host`
- `Resolver.appEnv`

The general helpers work on any environment, while the host split helpers only make sense when the
environment is actually a `HostContext`.

## When To Stop

If the split only exists because it sounds cleaner, stop and use a concrete record.

`HostContext` is worth using when your app services need a separate lane from the host and you want
to read them through `Flow.readHost` and `Flow.readAppEnv`.

Keep the adapter layer at the boundary that creates the `HostContext`; do not thread it through
every helper just because it is available.

See the [HostContext reference](../../reference/runtime/) for the constructors and mapping
helpers, and the [Capability reference](../../reference/capability/) for the `host`,
`appEnv`, and `resolve` readers.
