---
title: Resources and Results
---

# Resources and Results

## Own a resource for the life of a stream

A stream over a file, a socket, or a database cursor has to close it when consumption ends, however it ends.
`FlowStream.using` acquires a `Resource` on the first pull and releases it when the consuming Flow finishes: when the
stream is exhausted, when it fails, when it is interrupted, or when `take` stops it early.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let lines (path: string) =
    FlowStream.using (openReader path) (fun reader ->
        FlowStream.repeatFlow (Flow.fromBlocking (fun _ -> reader.ReadLine()))
        |> FlowStream.takeWhile (isNull >> not))
```

`FlowStream.repeatFlow` runs its flow once per pull, forever, so it never runs ahead of the consumer. Bound it with
`take` or `takeWhile`, or pace it with `throttle`.

## Take one result

Some consumers only need part of a stream:

- `FlowStream.runTryHead` pulls a single value and returns it, or `None` for an empty stream. It stops the stream as
  soon as the value arrives, so resources and producer fibers are released right away.
- `FlowStream.runTryLast` consumes the whole stream and returns its last value, or `None`.
- `FlowStream.runCount` consumes the whole stream and returns how many values it emitted.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let! firstMatch = commits |> FlowStream.filter matches |> FlowStream.runTryHead
```
