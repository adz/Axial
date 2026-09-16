---
title: Batching and Parallelism
---

# Batching and Parallelism

Strict batches and continuously replenished parallel work have different observable schedules. Axial keeps them
separate rather than hiding both behaviors behind one operator.

## Strict batches

Use `chunkBySize` with `Flow.sequencePar` when each complete batch must finish before the next starts:

```fsharp no-check reason="Application-specific checking operation is described in the surrounding prose"
let checkedPages =
    pages
    |> FlowStream.chunkBySize 8
    |> FlowStream.mapFlow (List.map checkAndExtract >> Flow.sequencePar)
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

```fsharp no-check reason="Application-specific loading operation is described in the surrounding prose"
let loaded =
    ids
    |> FlowStream.mapFlowPar (Parallelism.bounded 8) loadCustomer
```

At most eight mappings are active or retained. Results are emitted in completion order, so a slow earlier input does
not block later results or failures. Consuming a result opens one slot and starts the next upstream mapping.

If consumption stops early, the terminal stream scope interrupts and awaits outstanding mapping fibers before
returning. This keeps parallel work bounded in both count and lifetime.

## Which should you choose?

Choose strict batches when order and batch barriers are part of the contract—for example, checkpointing one page group
before fetching the next. Choose `mapFlowPar` for worker-pool behavior where throughput matters and completion order is
acceptable.
