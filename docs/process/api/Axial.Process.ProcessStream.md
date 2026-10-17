---
title: Process streams
api:
  family: Axial.Process.ProcessStream
  sections:
    - id: transform
      title: Transform streams
      order: 10
      summary: Filter, map, or stop consumption after a prefix.
      members: [map, filter, take, takeWhile]
    - id: consume
      title: Consume a stream
      order: 20
      summary: Collect, fold, drain, or handle each output value.
      members: [runCollect, runDrain, runFold, runFoldFlow, runForEach, runForEachFlow]
    - id: adapt
      title: Adapt to FlowStream
      order: 30
      summary: Convert process output for composition with application streams.
      members: [toFlowStream]
---

# Consuming process output

`ProcessStream<'env, 'value>` represents output that can be transformed as the process runs. Construct one with
`streamStdoutLines` or another
`Process` stream constructor.

Transforms such as `map`, `filter`, and `take` belong to this module. Terminals such as `runCollect`, `runFold`, and
`runForEachFlow` return a `Flow`; they do not start execution. Run that Flow at a script boundary, or bind it in the
application Flow. Terminals that retain completion return it alongside the consumed value:

- `ProcessCompletion.Completed result` means the process completed and includes its `ProcessResult`.
- `ProcessCompletion.StoppedEarly` means a stream operator stopped consumption and Axial cleaned up the process.

`toFlowStream` adapts values to `FlowStream` for composition with application streams, but intentionally drops
successful process completion. Keep the `ProcessStream` terminal when completion matters.

See [Streaming output](/process/streaming.html) for examples, including effectful consumers and early stopping.
