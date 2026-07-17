---
title: Split A Larger Application
weight: 40
description: Use project references to keep wire, domain, application, and infrastructure code in their intended places.
type: docs
---


Namespaces organize names but do not stop references. Separate projects can prevent domain code from importing wire or
infrastructure types at all.

Here, **domain** means the business types and rules. **Application** means the code that coordinates business operations.
**Infrastructure** means database, filesystem, HTTP, and other operational implementations. The **host** starts the
process and connects those parts.

**Contracts** are the wire records and version declarations used to exchange or store data. They are not the same as
the domain types used by business code.

A useful starting point is:

```text
MyApp.Contracts
MyApp.Domain
MyApp.Application
MyApp.Infrastructure
MyApp.Host
```

## Keep the dependency direction small

```text
Contracts  -> Axial.Schema
Domain     -> Axial.ErrorHandling, optionally Axial.Schema
Application -> Domain, optionally Axial.Flow
Infrastructure -> Application
Host -> Contracts, Domain, Application, Infrastructure
```

The exact project names do not matter. The useful restriction is that Domain cannot reference Contracts,
Infrastructure, a database provider, or an HTTP host.

## Put each concern in one place

`Contracts` contains public wire records, generated schemas, codecs, version chains, and explicit mappings at the data
boundary.

`Domain` contains refined values, private aggregates, domain errors, and named transitions. It should not know whether a
value arrived through JSON, a database row, or a message.

`Application` coordinates domain operations and declares required repositories or gateways. Its Flow environment can be
a record of those dependencies.

`Infrastructure` implements repositories and gateways. It may use filesystem, database, HTTP, clock, and other
operational libraries that Domain cannot reference.

`Host` owns dependency injection, configuration, routing, process lifetime, and assembly of live environments.

## Keep service-provider lookup at the host

Resolve container-managed objects while constructing the application environment. Application workflows then use typed
fields or `Service.get`, not arbitrary lookups.

```fsharp
type AppEnv =
    { Bookings: IBookingRepository
      Clock: IClock }

let env =
    { Bookings = provider.GetRequiredService<IBookingRepository>()
      Clock = provider.GetRequiredService<IClock>() }
```

Business workflows now state dependencies in `Flow<AppEnv, _, _>` and tests supply a record of fakes.

## Start smaller when appropriate

A small application can keep these as modules in one project. Preserve the same direction in file order and module
interfaces, then split projects when accidental imports or unclear ownership begin to cause review problems.

Project separation is useful when it removes choices from normal development. It is not a goal by itself.
