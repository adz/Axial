# Where work runs

Axial starts work inline. Say where CPU-heavy or blocking work runs with a `RunOn` — on one flow with `Flow.runOn`, or on a parallel batch through its `Parallelism`.

| Workload | Choice |
|---|---|
| Cheap synchronous work | `RunOn.Inline` or `Parallelism.bounded n` |
| Naturally waiting I/O | `Parallelism.io n` |
| CPU-heavy work | `RunOn.ThreadPool` or `Parallelism.cpu` |
| Synchronous blocking work | `RunOn.DedicatedThread` or `Parallelism.blocking n` |
| Unknown workload | `RunOn.adaptive ()` or `Parallelism.adaptive n` |
| A required .NET scheduler | `RunOn.Scheduler scheduler` |

`Flow.runOn placement source` controls one flow. `Flow.traversePar`, `Flow.traverseParUsing`, `Flow.forEachPar`, and `Flow.sequenceParWith` apply the placement carried by their `Parallelism` to each item while preserving the bound.

Wrap CPU-heavy branches individually so they can start concurrently:

```fsharp transcript
> let square value : Flow<string, int> = Flow.delay (fun () -> Flow.ok (value * value)) |> Flow.runOn RunOn.ThreadPool;;
> [ 1; 2; 3 ] |> List.map square |> Flow.sequencePar |> Flow.run ();;
val it: Exit<int list,string> = Success [1; 4; 9]
```

Adaptive placement starts inline and measures only how long invoking a flow holds the calling thread. Time after the flow returns an incomplete task is not counted, so waiting operations remain inline. The default samples three invocation attempts and compares their minimum hold time with a 50 microsecond threshold. Using the minimum prevents one cold/JIT/setup outlier from moving otherwise waiting work to the pool; every sample must exceed the threshold before the learner chooses `ThreadPool`. Failed and interrupted invocation attempts count as samples because they still held the calling thread. The decision is permanent.

Each `RunOn.adaptive ()` call owns separate memory; `Parallelism.adaptive n` creates a new learner each time the traversal runs. Use `RunOn.adaptiveWith { Threshold = threshold; Samples = samples }` to change the default. On JavaScript every placement, including adaptive placement, runs inline.
