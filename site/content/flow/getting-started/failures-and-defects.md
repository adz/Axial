---
weight: 8
title: Failures and Defects
description: Distinguish expected failures, unexpected defects, and interruption.
type: docs
---


A Flow has a typed channel for failures the caller is expected to handle:

```fsharp
type PaymentError = CardDeclined | AccountClosed

let charge : Flow<PaymentError, Receipt> =
    Flow.fail CardDeclined
```

Unexpected exceptions are defects. They are retained in the execution outcome rather than being added silently to
the workflow's typed error:

```fsharp
Flow.die (InvalidOperationException "broken invariant")
```

An Exit distinguishes the cases:

| Cause | Meaning |
| --- | --- |
| `Cause.Fail error` | Expected typed failure |
| `Cause.Die exception` | Unexpected defect |
| `Cause.Interrupt` | Cooperative cancellation or interruption |
| `Cause.Then (first, second)` | Sequentially combined causes |
| `Cause.Both (left, right)` | Concurrently combined causes |
| `Cause.Traced (cause, trace)` | Cause with diagnostic context |

Use an `attempt` constructor only when an exception from an interop API is an expected outcome. Use `Flow.catch` only
when the application deliberately translates a defect into a typed error.

`Exit.toResult` is intentionally lossy. Use it only at a boundary that has decided how defects, interruption, and
combined causes should be represented.

## Go Further

- [Defects]({{< relref "/flow/core-concepts/defects/" >}}) covers exception capture and intentional recovery in detail.
- [Cause reference]({{< relref "/flow/reference/cause/" >}}) lists cause transformations and rendering.
- [Supervision]({{< relref "/flow/concurrency/supervision/" >}}) explains how unjoined child defects are reported.
