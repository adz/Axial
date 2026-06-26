---
title: "Construction"
type: docs
---

This page shows the helpers that create or adapt flows before you start composing them with domain logic.

- [`Flow.Flow.ok`](./m-flow-flow-ok.md): Creates a successful synchronous flow.
- [`Flow.Flow.error`](./m-flow-flow-error.md): Creates a failing synchronous flow.
- [`Flow.Flow.succeed`](./m-flow-flow-succeed.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.Flow.value`](./m-flow-flow-value.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.Flow.fail`](./m-flow-flow-fail.md): Alias for <code>error</code> that reads well in some call sites.
- [`Flow.Flow.fromResult`](./m-flow-flow-fromresult.md): Lifts a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into a synchronous flow.
- [`Flow.Flow.fromOption`](./m-flow-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`Flow.Flow.fromValueOption`](./m-flow-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
- [`Flow.Flow.fromAsync`](./m-flow-flow-fromasync.md): Creates a flow from a raw async operation.
- [`Flow.Flow.attemptAsync`](./m-flow-flow-attemptasync.md): Creates a flow from an async operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.Flow.fromTask`](./m-flow-flow-fromtask.md): Creates a flow from a raw task operation.
- [`Flow.Flow.attemptTask`](./m-flow-flow-attempttask.md): Creates a flow from a task operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.Flow.fromValueTask`](./m-flow-flow-fromvaluetask.md): Creates a flow from a raw value task operation.
- [`Flow.Flow.attemptValueTask`](./m-flow-flow-attemptvaluetask.md): Creates a flow from a value task operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.Flow.orElseFlow`](./m-flow-flow-orelseflow.md): Turns a pure validation result into a synchronous flow with environment-provided failure.
- [`Flow.Flow.delay`](./m-flow-flow-delay.md): Defers flow construction until execution time.
