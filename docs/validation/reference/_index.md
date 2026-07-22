---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for `Axial.Validation`. The package keeps focused namespaces for checks and Results
(`Axial.ErrorHandling`), accumulated diagnostics (`Axial.Validation`), and refined values (`Axial.Refined`).

- [`Check`](./check/) — reusable structured value constraints returning `Result<unit, CheckFailure list>`.
- [`Predicate`](./predicate/) — plain `bool` facts for local branching, including the `PredicateExtensions` members.
- [`Result`](./result/) — focused helpers, guards, and the `result {}` builder over standard F# `Result`.
- [`Validation`](./validation/) — accumulating validation and the `validate {}` builder (namespace `Axial.Validation`).
- [`Diagnostics`](./diagnostics/) — path-aware error trees: inspection, merging, and rendering.
- [`Refined`](./refined/) — parse and refine single values into types that carry their own proof (namespace
  `Axial.Refined`).
