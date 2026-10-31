---
title: Platform Services
description: Explicit clock, logging, randomness, and GUID capabilities for Axial workflows.
---

# Platform services

Platform services expose host capabilities that are runtime-scoped rather than application authority. They support
Axial Core rather than defining separate documentation contexts.

Other packages build on these capabilities and have their own guides and API references:

- [Console](/console/): standard streams, redirection, terminal control, and test doubles.
- [FileSystem](/filesystem/): file and directory access, streaming traversal, atomic writes, and the append log.
- [Configuration](/configuration/): layered environment, argument, JSON, browser, and host configuration.
- [WebSocket](/websocket/): typed WebSocket client sessions.
- [Process](/process/): external process composition and streaming.
- [HTTP](/http/): typed requests, responses, and reliability.

| Service | Wraps | Axial API |
| --- | --- | --- |
| [Clock](platform-services/clock.html) | `DateTimeOffset.UtcNow` | Current time and derived Unix-time readers |
| [Logging](platform-services/logging.html) | Host logging | Structured messages and exception-preserving logging |
| [Randomness and GUIDs](platform-services/random-and-guid.html) | `System.Random`, `Guid.NewGuid()` | Explicit random values, byte generation, and GUID creation |
| [Culture, time zone, and formatting](platform-services/index.html#culture-and-time-zone) | `CultureInfo`, `TimeZoneInfo` | Explicit culture and zone values with pure formatting |
| [Using existing services](existing-services.html) | Application authority and runtime values | Compose explicit dependencies with runtime clock, random, GUID, and log values |

```fsharp
open Axial
```

A workflow declares the authority it needs. Runtime services such as `Clock.now` need no environment member; package
services such as `Console.writeLine` constrain the environment to their `IHas*` contract.

## In this section

1. [Platform-service bundle](platform-services/index.html): clock, logging, randomness, culture, time zone, and formatting.
2. [Using existing services](existing-services.html): compose application dependencies with runtime values.

[Telemetry](/telemetry/) instruments the runtime rather than defining a workflow dependency. [Hosting](/hosting/)
supplies environments and process/server lifecycle integration.
