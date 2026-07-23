---
weight: 90
title: FsToolkit.ErrorHandling
description: How Axial Result, Refined, Schema, and Flow relate to FsToolkit.ErrorHandling.
type: docs
---


FsToolkit.ErrorHandling provides a broad set of combinators and computation expressions for `Result`, asynchronous
results, validation, and related standard F# types.

Axial separates four roles:

- `Axial.Result` contains a smaller `Result` surface, `Check`, predicates, and `result { }`.
- `Axial.Refined` constructs values whose types record successful checks.
- `Axial.Schema` declares structured boundaries and accumulates path-aware failures.
- `Axial.Flow` runs effectful workflows with explicit dependencies.

Existing FsToolkit Result helpers can remain in an application. Both libraries use the standard F# `Result` type.

| FsToolkit pattern | Axial equivalent |
| --- | --- |
| `Result.requireTrue` | `Result.requireTrue` |
| `Result.requireSome` | `Result.someOr` |
| `result { }` | `result { }` |
| `asyncResult { }`, `taskResult { }` | `flow { }` |
| accumulating validation over boundary fields | a record `schema<'model> { }` interpreted by `Schema.parse` or `Schema.check` |

Schema adds one property that Result combinators do not provide: the declaration is inspectable. The same field and
constraint metadata can parse input, return complete paths, emit JSON Schema/OpenAPI, compile a JSON codec, and drive
forms or documentation.
