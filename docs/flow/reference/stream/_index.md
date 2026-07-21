---
title: "Stream"
weight: 130
---

This page shows the Fable-compatible `FlowStream` surface for cold, pull-based streams that participate in Axial's environment, typed-error, cancellation, and scope model. Construct streams from values or effectful state transitions, transform them without starting work, and consume them back into an ordinary `Flow`.

## Core type

- [`Flow.FlowStream`](./t-flow-flowstream.md):
 Represents a cold stream of values that requires an environment, can fail with a typed error,
 and supports backpressure.


## Construction

- [`Flow.FlowStream.empty`](./m-flow-flowstream-empty.md): Creates an empty stream.
- [`Flow.FlowStream.singleton`](./m-flow-flowstream-singleton.md): Creates a stream containing one value.
- [`Flow.FlowStream.fromSeq`](./m-flow-flowstream-fromseq.md): Creates a stream from a synchronous sequence of values.
- [`Flow.FlowStream.fromFlow`](./m-flow-flowstream-fromflow.md): Creates a one-element stream from an effectful value.
- [`Flow.FlowStream.unfoldFlow`](./m-flow-flowstream-unfoldflow.md): Creates a cold stream by repeatedly running an effectful state transition.

## Transformation

- [`Flow.FlowStream.map`](./m-flow-flowstream-map.md): Transforms the successful values of a stream using the provided function.
- [`Flow.FlowStream.mapError`](./m-flow-flowstream-maperror.md): Transforms the typed error channel of a stream.
- [`Flow.FlowStream.filter`](./m-flow-flowstream-filter.md): Keeps values that satisfy a predicate.
- [`Flow.FlowStream.choose`](./m-flow-flowstream-choose.md): Maps and filters values in one operation.
- [`Flow.FlowStream.mapFlow`](./m-flow-flowstream-mapflow.md): Transforms every value with a Flow effect.
- [`Flow.FlowStream.tapFlow`](./m-flow-flowstream-tapflow.md): Runs an effect for each value before emitting the original value.
- [`Flow.FlowStream.take`](./m-flow-flowstream-take.md): Emits at most <span class="fsdocs-param-name">count</span> values.
- [`Flow.FlowStream.skip`](./m-flow-flowstream-skip.md): Skips the first <span class="fsdocs-param-name">count</span> values.
- [`Flow.FlowStream.takeWhile`](./m-flow-flowstream-takewhile.md): Emits values while a predicate remains true.
- [`Flow.FlowStream.skipWhile`](./m-flow-flowstream-skipwhile.md): Skips values while a predicate remains true.
- [`Flow.FlowStream.indexed`](./m-flow-flowstream-indexed.md): Emits each value paired with its zero-based index.
- [`Flow.FlowStream.scan`](./m-flow-flowstream-scan.md): Emits successive accumulator states.
- [`Flow.FlowStream.distinctUntilChangedBy`](./m-flow-flowstream-distinctuntilchangedby.md): Suppresses consecutive duplicate values according to a projection.

## Composition

- [`Flow.FlowStream.append`](./m-flow-flowstream-append.md): Concatenates two streams, evaluating the second only after the first ends.
- [`Flow.FlowStream.collect`](./m-flow-flowstream-collect.md): Maps each value to a stream and concatenates the resulting streams.
- [`Flow.FlowStream.zip`](./m-flow-flowstream-zip.md): Pairs values from two streams until either stream ends.

## Consumption

- [`Flow.FlowStream.runForEach`](./m-flow-flowstream-runforeach.md): Executes the stream and performs a synchronous action for each successful value.
- [`Flow.FlowStream.runForEachFlow`](./m-flow-flowstream-runforeachflow.md): Runs an effectful action for every stream value.
- [`Flow.FlowStream.runFold`](./m-flow-flowstream-runfold.md): Folds a stream into one value inside Flow.
- [`Flow.FlowStream.runCollect`](./m-flow-flowstream-runcollect.md): Collects all emitted values into a list.
- [`Flow.FlowStream.runDrain`](./m-flow-flowstream-rundrain.md): Consumes a stream and ignores its values.
