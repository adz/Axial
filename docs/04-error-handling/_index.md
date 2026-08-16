---
title: Error Handling
description: Distinguish expected failures, unexpected defects, and interruption.
---

# Error Handling

A Flow has a typed channel for failures the caller is expected to handle:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
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

## Learn more

- [Bind](/error-handling/bind.html) explains how to assign or map an error at a `flow { }` bind site.
- [Policy and verification](/error-handling/policy.html) explains reusable verification rules and `Flow.verify`.
- [Defects](/error-handling/defects.html) covers exception capture and intentional recovery in detail.
- [Cause reference](/api.html) lists cause transformations and rendering.
- [Supervision](/concurrency-and-state/supervision.html) explains how unjoined child defects are reported.
