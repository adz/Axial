# Validation Path Scoping

## Extracted From

- `dev-docs/PLAN.md`
- `dev-docs/TASKS.md`

## Source Date

- 2026-05-06: validation graphs need path-scoped helpers to become usable in real user code

## Decision

Axial keeps `Diagnostics<'error>` as the explicit tree-shaped validation graph, with:

- `Errors` holding diagnostics attached to the current node
- `Children` holding nested branches keyed by `PathSegment`
- `Diagnostics.flatten` converting the tree into a linear list for reporting or tests

The current `validate {}` builder remains root-local:

- sibling failures accumulate in the current node’s `Errors` list
- the builder does not invent `Key`, `Index`, or `Name` branches on its own

To make the graph useful, Axial should expose scoped validation helpers that prefix a sub-validation with a path segment.
The preferred surface is a builder-scoped or companion API such as:

- `validate.key "customer" { ... }`
- `validate.index 0 { ... }`
- `validate.name "Email" { ... }`

These scoped helpers should:

- run a normal validation block
- prefix any diagnostics produced by that block with the supplied path segment
- compose naturally with `let!`, `and!`, `return!`, and helper validations

## Why

- Users should validate values, not hand-build diagnostics trees.
- Root-local accumulation is enough for flat forms, but not enough for nested data models.
- The tree shape is valuable only if there is a practical way to produce nested branches from real validation code.
- A scoped helper keeps the `validate {}` ergonomics while making the graph path-aware.

## Consequences

- Narrative docs should distinguish root-local accumulation from path-scoped validation branches.
- Examples should show `validate {}` for root-level `Errors` accumulation and scoped helpers for nested branches.
- `Diagnostics` remains a public tree type, but it should not be the primary construction surface.
- If later APIs are added for branch scoping, they should build on this model rather than replacing the graph.
