---
title: "FsFlow.DiagnosticsModule.merge"
linkTitle: "merge`"
---

Recursively merges two diagnostics graphs, combining shared branches and local errors.

## Remarks

This is the core operation for applicative validation. It ensures that errors from sibling
 fields are collected together into a single structured graph.


## Parameters

- `left`: The first graph of type `Diagnostics`.
- `right`: The second graph of type `Diagnostics`.

## Returns

A new `Diagnostics` containing the union of both inputs.

