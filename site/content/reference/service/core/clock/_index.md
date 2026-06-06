---
title: "Clock"
type: docs
---

This page shows the `Core.Clock` helpers for reading time from an explicit clock service.

- [`Core.Clock.now`](./m-core-clock-now.md): Reads the current UTC timestamp from an explicit clock service.
- [`Core.Clock.utcDateTime`](./m-core-clock-utcdatetime.md): Reads the current UTC date/time from an explicit clock service.
- [`Core.Clock.unixTimeSeconds`](./m-core-clock-unixtimeseconds.md): Reads the current Unix timestamp in seconds from an explicit clock service.
- [`Core.Clock.unixTimeMilliseconds`](./m-core-clock-unixtimemilliseconds.md): Reads the current Unix timestamp in milliseconds from an explicit clock service.
- [`Core.Clock.live`](./p-core-clock-live.md): Creates a live clock backed by <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow">DateTimeOffset.UtcNow</a>.
- [`Core.Clock.layer`](./p-core-clock-layer.md): Builds the live clock as a layer.
- [`Core.Clock.fromValue`](./m-core-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.
