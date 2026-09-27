---
title: Parallel
description: "Bounded parallel traversals, pooled resources, fail-fast zips, and races, with random bounds and random durations."
---

# Parallel

This scenario checks the [parallel combinators](/the-flow-type/combining-flows.html) with a random bound and random
work durations.

Run it with `dotnet run --project examples/Axial.TortureTest -- parallel 100`.

## What it does

- `Flow.traversePar` and `Flow.forEachPar` run hundreds of items with a random `Parallelism` bound, counting how many
  run at once.
- `Flow.traverseParUsing` and `Flow.forEachParUsing` give each running item a resource from a pool and record whether
  two items ever held the same one.
- One random item of a traversal fails.
- `Flow.zipPar` runs a failing side against one that never ends, and two failing sides. `Flow.sequencePar` runs items of
  random length.
- Fifty `Flow.race`s run two sides of random length, each signalling a `Deferred` when it ends.

{{< snippet id="torture-parallel" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| A bounded traversal never ran more than its bound, and kept input order | The bound is respected, and results come back in input order whatever order they finish in. |
| forEachPar processed every item exactly once | No item is skipped or repeated. |
| No pooled resource was used by two items at once, and every one was released | A resource is held by one item at a time, and the pool releases everything it acquired. |
| A failing item failed the traversal, and every started item cleaned up | Fail-fast interrupts the other items, and their cleanup still runs. |
| zipPar failed fast and interrupted the other side | A failure does not wait for the other side to finish on its own. |
| zipPar reported the failures that happened | Failures from both sides are kept in the cause. |
| sequencePar kept input order | Results are in input order. |
| Every race returned a side's value | A race returns its winner's value. |
| Both sides of every race finished and ran their exit handler | The loser is interrupted and settles; a race never leaves it running. |
