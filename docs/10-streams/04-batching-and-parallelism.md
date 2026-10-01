---
title: Batching and Parallelism
---

# Batching and Parallelism

Strict batches and continuously replenished parallel work have different observable schedules. Axial keeps them
separate rather than hiding both behaviors behind one operator.

## Strict batches

Use `FlowStream.chunkBySize` with `Flow.sequencePar` when each complete batch must finish before the next starts:

```fsharp transcript
> open Axial.PlatformService;;
> let completeAfter (label, milliseconds: int) =
-     Flow.sleep (System.TimeSpan.FromMilliseconds(float milliseconds)) |> Flow.map (fun () -> label)
- in
- (FlowStream.fromSeq [ ("A", 80); ("B", 10); ("C", 80); ("D", 10) ]
-  : FlowStream<ClockEnvironment, Never, string * int>)
- |> FlowStream.chunkBySize 2
- |> FlowStream.mapFlow (List.map completeAfter >> Flow.sequencePar)
- |> FlowStream.runCollect
- |> Flow.run (ClockEnvironment Clock.live);;
val it: Exit<string list list,Never> = Success [["A"; "B"]; ["C"; "D"]]
```

B and D complete first inside their respective batches, but `Flow.sequencePar` restores input order. The resulting
nested list also exposes the batch barrier: C and D belong to the second result only after the first batch completes.

This pipeline:

- pulls at most two source values for each batch in this example;
- runs those two mappings concurrently;
- returns results in input order;
- does not begin the next batch until the current result list is consumed;
- interrupts sibling mappings when one fails.

`Flow.sequencePar` runs every Flow in the supplied list, so `FlowStream.chunkBySize` supplies the bound.

## Continuously replenished work

Use `FlowStream.mapFlowPar` when a completed mapping should immediately open capacity for another input:

```fsharp transcript
> open Axial.PlatformService;;
> let completeAfter (label, milliseconds: int) =
-     Flow.sleep (System.TimeSpan.FromMilliseconds(float milliseconds)) |> Flow.map (fun () -> label)
- in
- (FlowStream.fromSeq [ ("A", 150); ("B", 10); ("C", 10) ]
-  : FlowStream<ClockEnvironment, Never, string * int>)
- |> FlowStream.mapFlowPar (Parallelism.bounded 2) completeAfter
- |> FlowStream.runCollect
- |> Flow.run (ClockEnvironment Clock.live);;
val it: Exit<string list,Never> = Success ["B"; "C"; "A"]
```

With two slots, completion of B starts C without waiting for A. The returned list makes completion-order emission
visible: B and C are emitted before the earlier but slower A.

At most two mappings are active or retained in this example. Results are emitted in completion order, so a slow earlier input does
not block later results or failures. Consuming a result opens one slot and starts the next upstream mapping.

If consumption stops early, the terminal stream scope interrupts and awaits outstanding mapping fibers before
returning. This keeps parallel work bounded in both count and lifetime.

## Which should you choose?

Choose strict batches when order and batch barriers are part of the contract, for example checkpointing one page group
before fetching the next. Choose `FlowStream.mapFlowPar` for worker-pool behavior where throughput matters and completion order is
acceptable.
