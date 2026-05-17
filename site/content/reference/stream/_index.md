---
title: "Stream"
weight: 120
type: docs
---

This page shows the `FlowStream` surface for cold, pull-based streams that still participate in FsFlow's environment and typed-error model. A stream can require `env`, emit values incrementally, and fail with the same error discipline as `Flow`. Use it when the boundary produces many values over time, such as file records, network messages, or paged API results. Keep per-item logic small and push final side effects through `runForEach` so cancellation and failure stay visible.

## Core type

- [`FlowStream`](./t-flowstream.md): 
 Represents a cold stream of values that requires an environment, can fail with a typed error,
 and supports backpressure.
 

## Module functions

- [`FlowStream.fromSeq`](./m-flowstream-fromseq.md): Creates a stream from a synchronous sequence of values.
- [`FlowStream.map`](./m-flowstream-map.md): Transforms the successful values of a stream using the provided function.
- [`FlowStream.runForEach`](./m-flowstream-runforeach.md): Executes the stream and performs a synchronous action for each successful value.

