---
weight: 40
title: Stream (FlowStream)
description: Effectful, pull-based asynchronous streams in FsFlow.
---

# Stream (FlowStream)

`FlowStream<'env, 'error, 'value>` represents a **cold**, pull-based asynchronous stream. Like `Flow`, a `FlowStream` requires an environment to run and can fail with a typed error. It is built on top of .NET's `IAsyncEnumerable`, providing native support for backpressure and asynchronous iteration.

> **Note:** `FlowStream` is currently available on **.NET** only.

## Creating a Stream

### From a Sequence
The simplest way to create a stream is from an existing `seq`.

```fsharp
let numbers = FlowStream.fromSeq [ 1 .. 10 ]
```

## Transforming Streams

### Mapping Values
You can transform the successful values in a stream using `FlowStream.map`.

```fsharp
let doubled = 
    numbers 
    |> FlowStream.map (fun x -> x * 2)
```

## Consuming Streams

To consume a stream, you use one of the execution helpers. These helpers turn the stream into a `Flow` that you can then run or compose.

### `runForEach`
Executes an action for every successful value in the stream. If the stream encounters a failure, execution stops and the `Flow` returns that failure.

```fsharp
let printNumbers =
    FlowStream.runForEach () (printfn "Value: %d") numbers
```

## Why use FlowStream?

`FlowStream` is designed for scenarios where you need to process large amounts of data without loading everything into memory at once. Because it is part of the FsFlow family, it integrates perfectly with your existing environments, errors, and cancellation logic.

- **Environment-Aware**: Can read dependencies like databases or APIs during iteration.
- **Typed Failures**: Handles errors consistently with the rest of your application.
- **Cancellable**: Automatically respects the `CancellationToken` provided to `Flow.run`.

## API Reference: Module `FlowStream`

| Function | Signature | Description |
| :--- | :--- | :--- |
| `fromSeq` | `seq<'v> -> FlowStream<'e, 'err, 'v>` | Creates a stream from a synchronous sequence. |
| `map` | `('v -> 'w) -> FlowStream<'e, 'err, 'v> -> FlowStream<'e, 'err, 'w>` | Transforms the values in the stream. |
| `runForEach` | `'env -> ('v -> unit) -> FlowStream<'e, 'err, 'v> -> Flow<'env, 'err, unit>` | Consumes the stream with a side-effecting action. |

---

## Next Steps

Explore more advanced concurrency patterns in [State and Concurrency]({{< relref "/docs/state-concurrency/" >}}).
