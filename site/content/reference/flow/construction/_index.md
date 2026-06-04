---
title: "Construction"
type: docs
---

This page shows the helpers that create or adapt flows before you start composing them with domain logic.

- [`Flow.ok`](./m-flow-ok.md): Creates a successful synchronous flow.
- [`Flow.error`](./m-flow-error.md): Creates a failing synchronous flow.
- [`Flow.succeed`](./m-flow-succeed.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.value`](./m-flow-value.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.fail`](./m-flow-fail.md): Alias for <code>error</code> that reads well in some call sites.
- [`Flow.fromResult`](./m-flow-fromresult.md): Lifts a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into a synchronous flow.
- [`Flow.fromOption`](./m-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`Flow.fromValueOption`](./m-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
- [`Flow.fromAsync`](./m-flow-fromasync.md): Creates a flow from a raw async operation.
- [`Flow.fromTask`](./m-flow-fromtask.md): Creates a flow from a raw task operation.
- [`Flow.fromValueTask`](./m-flow-fromvaluetask.md): Creates a flow from a raw value task operation.
- [`Flow.orElseFlow`](./m-flow-orelseflow.md): Turns a pure validation result into a synchronous flow with environment-provided failure.
- [`Flow.delay`](./m-flow-delay.md): Defers flow construction until execution time.
