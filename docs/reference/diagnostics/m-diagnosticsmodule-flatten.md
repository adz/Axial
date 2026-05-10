---
title: "FsFlow.DiagnosticsModule.flatten"
linkTitle: "flatten`"
---

Flattens the structured diagnostics graph into a linear list of diagnostics.

## Remarks

During flattening, child paths are accumulated from the root down into each emitted diagnostic.
 The tree itself stores only local errors and child branches, while `Diagnostic`
 is reserved for reporting output.


## Parameters

- `graph`: The `Diagnostics` to flatten.

## Returns

A list of type `Diagnostic` list.

