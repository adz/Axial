---
title: Fable
description: Use process specifications with a supplied interpreter on non-native targets.
weight: 50
type: docs
---

[`ProcessSpec`]({{< relref "/flow/reference/service/process/t-flow-process-processspec.md" >}}), [`ProcessResult`]({{< relref "/flow/reference/service/process/t-flow-process-processresult.md" >}}), [`ProcessError`]({{< relref "/flow/reference/service/process/t-flow-process-processerror.md" >}}), the DSL, and [`IProcess`]({{< relref "/flow/reference/service/process/t-flow-process-iprocess.md" >}}) are target-neutral. A browser cannot start an operating-system process, so [`Process.live`]({{< relref "/flow/reference/service/process/m-flow-process-process-live.md" >}}) and native stream adapters are available only on .NET.

Fable applications can construct specifications and run them against an `IProcess` implementation that delegates execution to a worker or another host:

```fsharp
type AppEnvironment =
    { Process: IProcess }
    interface IHas<IProcess> with member this.Service = this.Process

let workflow =
    Process.command "device-tool" [ "inspect" ]
    |> Process.run<AppEnvironment>
```

An interpreter returns lazy `Flow<unit, ProcessError, ProcessResult>` and `FlowStream<unit, ProcessError, ProcessEvent>` values. It must not start work while constructing those values.
