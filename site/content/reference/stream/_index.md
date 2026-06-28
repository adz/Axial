---
title: "Stream"
weight: 130
type: docs
---

This page shows the `FlowStream` surface for cold, pull-based streams that still participate in Axial's environment and typed-error model. A stream can require `env`, emit values incrementally, and fail with the same error discipline as `Flow`. Use it when the boundary produces many values over time, such as file records, network messages, or paged API results. Keep per-item logic small and push final side effects through `runForEach` so cancellation and failure stay visible.

## Core type

- [`Flow.FlowStream`](./t-flow-flowstream.md):
 Represents a cold stream of values that requires an environment, can fail with a typed error,
 and supports backpressure.


## Module functions

- [`Flow.FlowStream.fromSeq`](./m-flow-flowstream-fromseq.md): Creates a stream from a synchronous sequence of values.
- [`Flow.FlowStream.map`](./m-flow-flowstream-map.md): Transforms the successful values of a stream using the provided function.
- [`Flow.FlowStream.runForEach`](./m-flow-flowstream-runforeach.md): Executes the stream and performs a synchronous action for each successful value.
