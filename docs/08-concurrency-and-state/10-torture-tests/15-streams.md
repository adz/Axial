---
title: Streams
description: "Every stream operator on random input: deterministic operators against the List model, concurrent and timed operators against the properties they promise."
---

# Streams

Each round feeds random input through every [stream](/streams/index.html) operator. Deterministic operators must agree
with the `List` functions they mirror. Concurrent and timed operators must keep the properties they promise, whatever
the timing. Streams stopped early or failing must release what they own.

Run it with `dotnet run --project examples/Axial.TortureTest -- streams 100`.

## What it does

- `map`, `filter`, `choose`, `scan`, `indexed`, `distinctUntilChangedBy`, `skip`, `take`, `chunkBySize`, `collect`,
  `append`, `zip`, `mapFlow`, `unfoldFlow`, and the `run*` consumers run on the same input as their `List`
  equivalents.
- `mapFlowPar` and `mapFlowParUsing` run with a random bound. `buffer` runs with a lossless, a sliding, and a dropping
  strategy against a source that pauses at random. `mergePar`, `groupedWithin`, `throttle`, `debounce`, and
  `switchMapFlow` run on the same pausing source.
- A stream that owns a resource through `FlowStream.using` is stopped early by `take`, and fails part way.

{{< snippet id="torture-streams" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Deterministic operators agree with the List model | Each operator computes what its `List` counterpart does. |
| mapFlowPar kept every value and its bound | Parallel mapping never exceeds its bound and loses nothing. |
| mapFlowParUsing never shared a resource and released every one | Each mapping holds its own resource. |
| A lossless buffer kept every value in order | `BackPressure` slows the producer instead of dropping. |
| A sliding buffer kept values in order, ending with the last | `Sliding` loses older values but always delivers the newest. |
| A dropping buffer kept values in order | `Dropping` loses newer values but never reorders. |
| mergePar kept every value and each source's order | Merging interleaves sources without losing or reordering any one of them. |
| groupedWithin grouped every value in order, within its size | Groups close on size or time, and no value is lost at a boundary. |
| throttle kept values in order, including the first and the last | Throttling drops values in between, never the first or the last. |
| debounce kept values in order, including the last | The final value always gets through. |
| switchMapFlow kept results in order, including the last | The newest value's flow always finishes, even when it arrives as a timer fires. |
| Stopping early or failing released every owned resource | `take` and a failure both close the stream's scope. |
| No stream fiber was left running | Every fiber a stream starts is interrupted and awaited before the consumer returns. |
