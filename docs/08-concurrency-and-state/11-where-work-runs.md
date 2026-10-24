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

Adaptive placement starts inline and measures only how long invoking a flow holds the calling thread. Time after the flow returns an incomplete task is not counted, so waiting operations remain inline. After its samples, the learner permanently chooses `Inline` or `ThreadPool`. Each `RunOn.adaptive ()` call owns separate memory; `Parallelism.adaptive n` creates a new learner each time the traversal runs.

Use `RunOn.adaptiveWith { Threshold = threshold; Samples = samples }` to change the default one sample and 50 microsecond threshold. On JavaScript every placement, including adaptive placement, runs inline.
