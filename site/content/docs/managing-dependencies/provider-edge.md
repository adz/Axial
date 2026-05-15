---
weight: 40
title: "Standard .NET AppHost Plus DI"
description: Using IServiceProvider only at the host boundary.
type: docs
---


`IServiceProvider` belongs at the outer edge of the system.

This gives you:

- host registrations that you can turn into typed values once
- a familiar entry point for ASP.NET, workers, and other DI-driven runtimes
- a boundary record or nominal contract that workflow code can consume without reaching back into
  `IServiceProvider`

This is not the primary dependency model for workflows. It is the bridge from host registrations
to typed contracts.

## The Lookup

`Resolver.fromProvider` asks `IServiceProvider` for a service and fails with `MissingCapability`
when the service is not registered.

```fsharp
let sendEmail : Flow<IServiceProvider, MissingCapability, IEmailSender> =
    Resolver.fromProvider<IEmailSender>
```

That shape is acceptable at the edge because it preserves the failure in the type.

## The Tradeoff

The win is ergonomics:

- very little boilerplate
- easy .NET host integration
- familiar to teams already using Microsoft DI

The cost is honesty:

- the workflow is typed against the provider, not the exact service set
- missing registrations surface at runtime

That is acceptable at the edge, not inside reusable helper code.

## Host To Boundary Record

The strongest use of `IServiceProvider` access is to map it into a boundary record or adapter once
and stop there.

```fsharp
type ApiDeps =
    { Orders : IOrderRepository
      Email : IEmailSender
      Clock : IClock }

let mapApiDeps (sp: IServiceProvider) =
    { Orders = sp.GetRequiredService<IOrderRepository>()
      Email = sp.GetRequiredService<IEmailSender>()
      Clock = sp.GetRequiredService<IClock>() }
```

Once you have the record, the rest of the workflow should usually stay on a named contract or on
the `HostContext` split.

## When Not To Use It

Do not use `IServiceProvider` as the default shape for reusable helpers.

If a helper is reusable, use:

- a boundary record
- `HostContext`
- or a small nominal interface contract

The host boundary is the edge, not the center. Keep it at the host boundary, adapt once, and then
stop reaching back into the container from workflow code.

See the [Capability reference](../../reference/capability/) for `Resolver.fromProvider` and
`MissingCapability`.
