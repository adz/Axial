---
title: Platform Boundary
---

# Platform Boundary

`FlowStream` is available on .NET and Fable. Its core operations describe pull scheduling, transformation, failure,
cancellation, and cleanup without performing I/O themselves.

Platform-specific packages provide sources:

- `Axial.Process` launches and observes .NET processes.
- A browser adapter can turn events or a readable stream into a `FlowStream`.
- A Node adapter can connect child-process or socket callbacks.
- Application adapters can expose database cursors, queues, or paginated clients.

Those adapters keep acquisition and callbacks at the host boundary. They expose values through `unfoldFlow` or another
resource-aware source and register owned handles with the active Flow scope.

Consumers remain portable because they receive an ordinary `FlowStream` and do not need to know which adapter produced
it. For a complete .NET example, see [Streaming OS process output](/process/streaming.html), where stdout, stderr, and
process completion become explicit stream values.

Do not start platform work while constructing the stream. Opening a handle, registering a callback, or launching a
process happens only when a terminal Flow begins consumption.
