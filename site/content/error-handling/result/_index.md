---
weight: 8
title: Result
type: docs
description: Fail-fast Result helpers, reusable checks, predicates, and the result computation expression.
---


`Axial.Result` adds helpers around the standard F# `Result<'value, 'error>` type. It also contains reusable value
guards and the `result {}` computation expression.

Use Result when later work depends on earlier success and the first error should stop the operation.

## Installation

`Axial.Result` installs as part of `Axial.ErrorHandling`, `Axial.Schema`, and `Axial`.

Or install it individually:

```sh
dotnet add package Axial.Result
```

## Pages

- [Result](../reference/result/): helpers for creating, transforming, extracting, and traversing Results.
- [Result CE](../result-builder/): sequence dependent Result-returning operations with `result {}`.
- [Constraint](../constraint/): reusable constraints that preserve their input on success.
- [Predicate](../constraint/overview/): boolean facts for local branching and checks.
