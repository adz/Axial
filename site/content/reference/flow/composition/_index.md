---
title: "Composition"
type: docs
---

This page shows the everyday Flow combinators for mapping, binding, zipping, and otherwise shaping workflow logic.

- [`Flow.map`](./m-flow-map.md): Transforms the successful value of a flow.
- [`Flow.bind`](./m-flow-bind.md): Sequences a dependent flow after a successful value.
- [`Flow.tap`](./m-flow-tap.md): Runs an effect on success and preserves the original value.
- [`Flow.tapError`](./m-flow-taperror.md): Runs a synchronous side effect on failure and preserves the original error.
- [`Flow.mapError`](./m-flow-maperror.md): Maps the error value of a synchronous flow.
- [`Flow.catch`](./m-flow-catch.md): Catches exceptions raised during execution and maps them to a typed error.
- [`Flow.orElseWith`](./m-flow-orelsewith.md): Computes a fallback flow from the typed error when the source flow fails.
- [`Flow.orElse`](./m-flow-orelse.md): Falls back to another flow when the source flow fails.
- [`Flow.zip`](./m-flow-zip.md): Runs two flows sequentially and combines their successful values into a tuple.
- [`Flow.map2`](./m-flow-map2.md): Combines two flows with a mapping function.
- [`Flow.map3`](./m-flow-map3.md): Combines three flows with a mapping function.
- [`Flow.apply`](./m-flow-apply.md): Applies a flow-wrapped function to a flow-wrapped value.
- [`Flow.ignore`](./m-flow-ignore.md): Maps the successful value of a synchronous flow to <code>unit</code>.
- [`Flow.traverse`](./m-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`Flow.sequence`](./m-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.
