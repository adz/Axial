# Flow comparisons

Seven scenarios where `Flow<'env, 'error, 'value>` improves a real program, each implemented twice with the
same domain types and service interfaces:

- `Ordinary` — `Task`, exceptions, cancellation tokens, and manually passed services.
- `WithFlow` — the Axial implementation.

The point is never that Flow makes side effects pure. It is that expected failure, dependencies, cancellation,
resource lifetime, and composition policy become visible in the signature and testable at the composition
point. Each file's header lists what is **made visible by the type**, what is **enforced by the runtime**, and
what is **still the application's responsibility**.

| File | Scenario | Axial surface exercised |
| --- | --- | --- |
| `CheckoutCompensation.fs` | Reserve/charge/ship with compensation | `Flow.acquireReleaseWith`, `Bind.mapError`, env records |
| `RetryBudget.fs` | HTTP fetch with a selective retry budget | `Axial.Flow.HttpClient`, `RetryPolicy`, `Flow.Runtime.retry`/`timeout` |
| `DashboardFanOut.fs` | Parallel fan-out with one optional branch | `Flow.zipPar`, `Flow.orElse`, `Flow.race`, `Axial.Flow.Telemetry` |
| `ScopedWorkspace.fs` | Temp workspace released exactly once | `Axial.Flow.FileSystem`, acquire/release lifetime |
| `ReportWiring.fs` | Wiring that cannot omit a capability | `IHas<>` env, `Layer.merge`, `Clock.fromValue`, `Axial.Flow.Console` |
| `OutputPipeline.fs` | Producer/consumer with interruption | `FlowStream`, `Axial.Flow.Process.Process.stream`, `Flow.fork`/`interrupt` |
| `InventoryStm.fs` | Contended reservation without locks | `STM`, `TRef`, `STM.retry`, `STM.orElse` |

Every claimed guarantee has a failure-path test in
[`tests/Axial.Flow.Comparisons.Tests`](../../tests/Axial.Flow.Comparisons.Tests/) that would fail if the
guarantee were removed: the finalizer runs on defects, the retry predicate is selective, the losing zipPar
branch is interrupted, the STM commit is atomic — and the ordinary versions include the classic bugs
(`checkoutBuggy`, `importBatchLeaky`) with tests proving the leak.

```bash
dotnet test tests/Axial.Flow.Comparisons.Tests --nologo
```

The corresponding walkthrough lives at `docs/flow/comparisons/task-vs-flow-scenarios.md`.
