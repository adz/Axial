---
title: "Error Handling: Result, checks, and refined values"
linkTitle: Error Handling
type: docs
notoc: true
description: Fail-fast results, reusable constraints, and domain values that record successful construction.
weight: 6
menu:
  main:
    weight: 4
---


Most F# code already returns `Result<'value,'error>`. The gap is everything around it: rules you want to reuse
across several fields, and values whose type should record that a rule already passed. Axial splits that gap into
three small, independently installable packages instead of one large validation library:

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Result` | Fail-fast `Result` composition, conversions, extraction helpers, and `result { }` | [Result](./result/) |
| `Axial.Check` | Reusable, path-free constraints over one typed value, returning the standard F# `Result` | [Check](./check/) |
| `Axial.Refined` | Parsing and constructing values whose types record successful checks | [Refined](./refined/) |

None of the three requires the others. `Axial.Check` and `Axial.Refined` do not depend on `Axial.Result` — already
use FsToolkit.ErrorHandling, or your own `Result` helpers? Add Check and/or Refined on their own, with no builder or
module ambiguity.

[Axial.Schema]({{< relref "/schema/" >}}) builds on Check and Refined for structured, path-aware boundaries.
[Axial.Flow]({{< relref "/flow/" >}}) uses ordinary `Result` for typed workflow failures; neither depends on Result,
Check, or Refined.

See [Result, Check, and Refined](./overview/) for installation commands and a first look at each package, or go
straight to the one you need.
