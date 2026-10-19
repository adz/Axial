---
title: Error Handling
description: Distinguish expected failures, unexpected defects, and interruption.
---

# Error Handling

```fsharp prepare
// Setup for the checked examples on this page.
open System
open Axial

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

A Flow has a typed channel for failures the caller is expected to handle:

```fsharp transcript
> type PaymentError = CardDeclined | AccountClosed;;
> let charge () : Flow<PaymentError, decimal> = Flow.fail CardDeclined;;
> Flow.run () (charge ());;
val it: Exit<decimal,PaymentError> = Failure (Fail CardDeclined)
```

Unexpected exceptions are defects. The execution outcome retains them outside the workflow's typed error channel:

```fsharp transcript
> open System;;
> type PaymentError = CardDeclined | AccountClosed;;
> let broken () : Flow<PaymentError, decimal> = Flow.die (InvalidOperationException "broken invariant");;
> match Flow.run () (broken ()) with Exit.Failure (Cause.Die error) -> error.Message | _ -> "";;
val it: string = "broken invariant"
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

Use [`Flow.recover`](/api/Axial.Flow.html#Axial.Flow.recover) when a typed failure needs a fallback with a different
error type, [`Flow.orElseWith`](/api/Axial.Flow.html#Axial.Flow.orElseWith) when it keeps the same error type, and
[`Flow.fold`](/api/Axial.Flow.html#Axial.Flow.fold) to inspect every cause. See
[Recovery and compound causes](recovery.html) for what happens when error types change and when a handler throws.

```fsharp
type PaymentError = CardDeclined | AccountClosed

let declined : Flow<PaymentError, decimal> = Flow.fail CardDeclined
let broken : Flow<PaymentError, decimal> = Flow.die (InvalidOperationException "boom")
```

```fsharp run
// orElseWith recovers a typed failure and keeps the same error type.
declined
|> Flow.orElseWith (fun _ -> Flow.ok 0m)
|> Flow.run ()
|> shouldEqual (Exit.Success 0m : Exit<decimal, PaymentError>)

// recover also recovers a typed failure but may change the error type.
declined
|> Flow.recover (fun _ -> Flow.ok 0m)
|> Flow.run ()
|> shouldEqual (Exit.Success 0m : Exit<decimal, Never>)

// Neither handles a defect; fold sees every cause, including defects.
broken
|> Flow.orElseWith (fun _ -> Flow.ok 0m)
|> Flow.run ()
|> function
| Exit.Failure(Cause.Die error) -> shouldEqual "boom" error.Message
| other -> failwithf "expected a defect, got %A" other

broken
|> Flow.fold (fun value -> Flow.ok $"value {value}") (fun cause -> Flow.ok (Cause.prettyPrint string cause))
|> Flow.run ()
|> shouldEqual (Exit.Success "Die(InvalidOperationException: boom)")
```

`Exit.toResult` loses information. Use it only at a boundary that has decided how to represent defects, interruption,
and combined causes.

## Learn more

- [Bind](/error-handling/bind.html) explains how to assign or map an error at a `flow { }` bind site.
- [Policy and verification](/error-handling/policy.html) explains reusable verification rules and `Flow.verify`.
- [Defects](/error-handling/defects.html) covers exception capture and intentional recovery in detail.
- [Cause reference](/api/) lists cause transformations and rendering.
- [Supervision](/concurrency-and-state/supervision.html) explains how unjoined child defects are reported.
