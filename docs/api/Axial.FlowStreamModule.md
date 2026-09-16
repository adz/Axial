# FlowStream

`FlowStream<'env, 'error, 'value>` describes a cold, pull-based sequence of effectful values. It uses the same
environment, typed failure channel, cancellation, and runtime scopes as `Flow`.

Choose `FlowStream` when values should be handled incrementally instead of collected before work begins. Typical
sources include process output, paginated APIs, subscriptions, and host-specific file or network adapters.

A stream does nothing until a terminal operation such as [`runFold`](#Axial.FlowStreamModule.runFold),
[`runForEachFlow`](#Axial.FlowStreamModule.runForEachFlow), or
[`runCollect`](#Axial.FlowStreamModule.runCollect) produces a Flow and that Flow is run. Pulling provides backpressure: downstream requests each next value, so upstream cannot run ahead
without an operator explicitly introducing bounded concurrency.

## Start here

The [Streams guide](/streams/index.html) introduces the model. [Getting started](/streams/getting-started.html) builds a
complete pipeline, while [Batching and parallelism](/streams/batching-and-parallelism.html) explains the scheduling
difference between strict batches and [`mapFlowPar`](#Axial.FlowStreamModule.mapFlowPar).

Use incremental terminal operations for large or unbounded streams. [`runCollect`](#Axial.FlowStreamModule.runCollect) deliberately retains every emitted
value and should be reserved for known finite inputs.
