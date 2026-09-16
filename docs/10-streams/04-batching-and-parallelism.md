---
title: Batching and Parallelism
---

# Batching and Parallelism

Strict batches and continuously replenished parallel work have different observable schedules. Axial keeps them
separate rather than hiding both behaviors behind one operator.

## Strict batches

Use `chunkBySize` with `Flow.sequencePar` when each complete batch must finish before the next starts:

```fsharp no-check reason="completeAfter is an illustrative timed Flow used to make scheduling visible"
FlowStream.fromSeq [ "A"; "B"; "C"; "D" ]
|> FlowStream.chunkBySize 2
|> FlowStream.mapFlow (List.map completeAfter >> Flow.sequencePar)
|> FlowStream.runForEach (printfn "%A")
```

Even if B finishes first, the observable batches retain input order and the second batch waits:

```text
start A, B
finish B
finish A
emit ["A"; "B"]
start C, D
finish D
finish C
emit ["C"; "D"]
```

This pipeline:

- pulls at most eight source values for the batch;
- runs those eight mappings concurrently;
- returns results in input order;
- does not begin the next batch until the current result list is consumed;
- interrupts sibling mappings when one fails.

`Flow.sequencePar` runs every Flow in the supplied list, so `chunkBySize` supplies the bound.

## Continuously replenished work

Use `mapFlowPar` when a completed mapping should immediately open capacity for another input:

```fsharp no-check reason="completeAfter is an illustrative timed Flow used to make scheduling visible"
FlowStream.fromSeq [ "slow A"; "fast B"; "fast C" ]
|> FlowStream.mapFlowPar (Parallelism.bounded 2) completeAfter
|> FlowStream.runForEach (printfn "%s")
```

With two slots, completion of B starts C without waiting for A:

```text
start slow A
start fast B
emit fast B
start fast C
emit fast C
emit slow A
```

At most two mappings are active or retained in this example. Results are emitted in completion order, so a slow earlier input does
not block later results or failures. Consuming a result opens one slot and starts the next upstream mapping.

If consumption stops early, the terminal stream scope interrupts and awaits outstanding mapping fibers before
returning. This keeps parallel work bounded in both count and lifetime.

## Which should you choose?

Choose strict batches when order and batch barriers are part of the contract—for example, checkpointing one page group
before fetching the next. Choose `mapFlowPar` for worker-pool behavior where throughput matters and completion order is
acceptable.
