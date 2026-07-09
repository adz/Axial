---
title: "Clock"
---

This page shows the `Core.Clock` helpers for reading time from an explicit clock service.

- [`Flow.PlatformService.Clock.now`](./m-flow-platformservice-clock-now.md): Reads the current UTC timestamp from an explicit clock service.
- [`Flow.PlatformService.Clock.utcDateTime`](./m-flow-platformservice-clock-utcdatetime.md): Reads the current UTC date/time from an explicit clock service.
- [`Flow.PlatformService.Clock.unixTimeSeconds`](./m-flow-platformservice-clock-unixtimeseconds.md): Reads the current Unix timestamp in seconds from an explicit clock service.
- [`Flow.PlatformService.Clock.unixTimeMilliseconds`](./m-flow-platformservice-clock-unixtimemilliseconds.md): Reads the current Unix timestamp in milliseconds from an explicit clock service.
- [`Flow.PlatformService.Clock.live`](./p-flow-platformservice-clock-live.md): Creates a live clock backed by <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow">DateTimeOffset.UtcNow</a>.
- [`Flow.PlatformService.Clock.layer`](./p-flow-platformservice-clock-layer.md): Builds the live clock as a layer.
- [`Flow.PlatformService.Clock.fromValue`](./m-flow-platformservice-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.
