---
title: "Diagnostics"
---

The `Diagnostics` type represents a structured graph of validation failures.

## Graph types

- [`FsFlow.PathSegment`](./t-pathsegment.md): Location markers used to describe where a diagnostic belongs in a validation graph.
- [`FsFlow.Path`](./t-path.md): A path through a validation graph, represented as a list of `PathSegment`.
- [`FsFlow.Diagnostic`](./t-diagnostic.md): A single failure item attached to a path in a validation graph.
- [`FsFlow.Diagnostics`](./t-diagnostics.md): A mergeable validation graph that carries local errors and nested child branches.

## Module functions

- [`FsFlow.DiagnosticsModule.empty`](./m-diagnosticsmodule-empty.md): Creates an empty diagnostics graph with no errors.
- [`FsFlow.DiagnosticsModule.singleton`](./m-diagnosticsmodule-singleton.md): Creates a diagnostics graph containing exactly one error at the root.
- [`FsFlow.DiagnosticsModule.merge`](./m-diagnosticsmodule-merge.md): Recursively merges two diagnostics graphs, combining shared branches and local errors.
- [`FsFlow.DiagnosticsModule.toString`](./m-diagnosticsmodule-tostring.md): Renders a diagnostics graph in a YAML-like layout for display.
- [`FsFlow.DiagnosticsModule.flatten`](./m-diagnosticsmodule-flatten.md): Flattens the structured diagnostics graph into a linear list of diagnostics.

