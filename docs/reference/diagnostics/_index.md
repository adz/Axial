---
title: "Diagnostics"
weight: 90
---

This page shows the diagnostics graph used by `Validation`. A `Diagnostics<'error>` value stores errors at the current node and at named, keyed, or indexed child paths, so validation can report both what failed and where it failed. Use `Diagnostics.singleton` for one error, `merge` to combine sibling reports, `flatten` when callers need path-bearing diagnostics, and `toString` for compact human-readable output. Keep diagnostics at the validation boundary; convert them to domain responses or UI messages at the edge.

## Graph types

- [`PathSegment`](./t-pathsegment.md): Location markers used to describe where a diagnostic belongs in a validation graph.
- [`Diagnostic.Path`](./t-path.md): The path to the source of the error.
- [`Diagnostic`](./t-diagnostic.md): A single failure item attached to a path in a validation graph.
- [`Diagnostics`](./t-diagnostics.md):
 A mergeable validation graph that carries local errors and nested child branches.


## Module functions

- [`Diagnostics.empty`](./m-diagnostics-empty.md): Creates an empty diagnostics graph with no errors.
- [`Diagnostics.singleton`](./m-diagnostics-singleton.md): Creates a diagnostics graph containing exactly one error at the root.
- [`Diagnostics.merge`](./m-diagnostics-merge.md): Recursively merges two diagnostics graphs, combining shared branches and local errors.
- [`Diagnostics.toString`](./m-diagnostics-tostring.md): Renders a diagnostics graph in a YAML-like layout for display.
- [`Diagnostics.flatten`](./m-diagnostics-flatten.md): Flattens the structured diagnostics graph into a linear list of diagnostics.
