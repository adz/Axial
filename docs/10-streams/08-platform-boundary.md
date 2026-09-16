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

Consumers remain portable:

```fsharp no-check reason="The platform-specific source is supplied by an adapter"
platformSource
|> FlowStream.map normalize
|> FlowStream.filter isRelevant
|> FlowStream.runForEachFlow handle
```

Do not start platform work while constructing the stream. Opening a handle, registering a callback, or launching a
process happens only when a terminal Flow begins consumption.
