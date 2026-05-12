---
title: "Stream"
type: docs
---

The `FlowStream` module provides asynchronous, pull-based streams.

## Core type

- [`FsFlow.FlowStream`](./t-flowstream.md): Represents a cold stream of values that requires an environment, can fail with a typed error,
 and supports backpressure.

## Module functions

- [`FsFlow.FlowStreamModule.fromSeq`](./m-flowstreammodule-fromseq.md): Creates a stream from a sequence of values.
- [`FsFlow.FlowStreamModule.map`](./m-flowstreammodule-map.md): Maps the successful values of a stream.
- [`FsFlow.FlowStreamModule.runForEach`](./m-flowstreammodule-runforeach.md): Executes the stream and performs an action for each value.

