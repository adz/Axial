---
title: "Diagnostics"
weight: 90
type: docs
---

This page shows the diagnostics graph used by `Validation`. A `Diagnostics<'error>` value stores errors at the current node and at named, keyed, or indexed child paths, so validation can report both what failed and where it failed. Use `Diagnostics.singleton` for one error, `merge` to combine sibling reports, `flatten` when callers need path-bearing diagnostics, and `toString` for compact human-readable output. Keep diagnostics at the validation boundary; convert them to domain responses or UI messages at the edge.

## Graph types

- [`Validation.PathSegment`](./t-validation-pathsegment.md): Location markers used to describe where a diagnostic belongs in a validation graph.
- [`Path`](./t-path.md): A path through a validation graph, represented as a list of <a href="t-validation-pathsegment.md">PathSegment</a>.
- [`Validation.Diagnostic`](./t-validation-diagnostic.md): A single failure item attached to a path in a validation graph.
- [`Validation.Diagnostics`](./t-validation-diagnostics.md):
 A mergeable validation graph that carries local errors and nested child branches.


## Module functions

- [`Validation.Diagnostics.empty`](./m-validation-diagnostics-empty.md): Creates an empty diagnostics graph with no errors.
- [`Validation.Diagnostics.singleton`](./m-validation-diagnostics-singleton.md): Creates a diagnostics graph containing exactly one error at the root.
- [`Validation.Diagnostics.merge`](./m-validation-diagnostics-merge.md): Recursively merges two diagnostics graphs, combining shared branches and local errors.
- [`Validation.Diagnostics.toString`](./m-validation-diagnostics-tostring.md): Renders a diagnostics graph in a YAML-like layout for display.
- [`Validation.Diagnostics.flatten`](./m-validation-diagnostics-flatten.md): Flattens the structured diagnostics graph into a linear list of diagnostics.
