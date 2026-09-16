---
title: Streams
description: Process effectful values incrementally with bounded memory and backpressure.
---

# Streams

A `Flow` produces one result. A `FlowStream` produces values over time without first loading all of them into memory.
Use it when the input may be large, slow, or unbounded: paginated APIs, message subscriptions, and incremental file
or network adapters.

```fsharp
FlowStream.fromSeq [ 1..6 ]
|> FlowStream.filter (fun number -> number % 2 = 0)
|> FlowStream.map (fun number -> number * 10)
|> FlowStream.runForEach (printfn "%d")
|> Flow.run ()
|> ignore
```

```text
20
40
60
```

The stream is **cold**: constructing it starts nothing. It is **pull-based**: downstream asks for each next value, so
upstream cannot outrun it. It shares Flow's environment, typed failures, cancellation, scopes, and structured fibers.

Start with [Getting started](getting-started.html). It builds one complete stream before the individual guides explain
each part.

## Guides

1. [Getting started](getting-started.html) — construct, transform, and consume one stream.
2. [Constructing streams](constructing.html) — existing values, one Flow, and effectful unfolding.
3. [Transforming values](transforming.html) — pure and effectful operators.
4. [Batching and parallelism](batching-and-parallelism.html) — strict batches versus continuously replenished work.
5. [Composing streams](composing.html) — append, flatten, and zip.
6. [Consuming streams](consuming.html) — folds, incremental effects, collection, and resource cleanup.
7. [Platform boundary](platform-boundary.html) — portable stream mechanics and host-specific adapters.

For one OS integration example, see the Process guide's [Streaming output](/process/streaming.html) page. It explains
how `Axial.Process` exposes stdout and stderr as a `FlowStream`; process handling is not part of the stream model.

The [`FlowStream` API](xref:Axial.FlowStreamModule) lists every operator after these guides establish the model.
