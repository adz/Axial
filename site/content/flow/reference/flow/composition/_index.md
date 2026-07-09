---
title: "Composition"
type: docs
---

This page shows the everyday Flow combinators for mapping, binding, zipping, and otherwise shaping workflow logic.

- [`Flow.verify`](./m-flow-flow-verify.md): Runs an environment-aware policy against an input value inside a workflow.
- [`Flow.map`](./m-flow-flow-map.md): Transforms the successful value of a flow.
- [`Flow.bind`](./m-flow-flow-bind.md): Sequences a dependent flow after a successful value.
- [`Flow.tap`](./m-flow-flow-tap.md): Runs an effect on success and preserves the original value.
- [`Flow.tapError`](./m-flow-flow-taperror.md): Runs a synchronous side effect on failure and preserves the original error.
- [`Flow.mapError`](./m-flow-flow-maperror.md): Maps the error value of a synchronous flow.
- [`Flow.catch`](./m-flow-flow-catch.md): Catches exceptions raised during execution and simple defect outcomes, then maps them to a typed error.
- [`Flow.orElseWith`](./m-flow-flow-orelsewith.md): Computes a fallback flow from the typed error when the source flow fails.
- [`Flow.orElse`](./m-flow-flow-orelse.md): Falls back to another flow when the source flow fails.
- [`Flow.zip`](./m-flow-flow-zip.md): Runs two flows sequentially and combines their successful values into a tuple.
- [`Flow.map2`](./m-flow-flow-map2.md): Combines two flows with a mapping function.
- [`Flow.map3`](./m-flow-flow-map3.md): Combines three flows with a mapping function.
- [`Flow.apply`](./m-flow-flow-apply.md): Applies a flow-wrapped function to a flow-wrapped value.
- [`Flow.ignore`](./m-flow-flow-ignore.md): Maps the successful value of a synchronous flow to <code>unit</code>.
- [`Flow.traverse`](./m-flow-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`Flow.sequence`](./m-flow-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.
