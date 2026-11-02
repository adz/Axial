---
title: Recovery and compound causes
linkTitle: Recovery and compound causes
description: Choose the recovery function, and understand what happens when error types change or a handler throws.
---

# Recovery and compound causes

Use [`Flow.recover`](/api/Axial.Flow.html#Axial.Flow.recover) when a typed failure needs a fallback with a different
error type. Like [`Flow.orElseWith`](/api/Axial.Flow.html#Axial.Flow.orElseWith), it handles a single typed failure
(including a traced one), not defects or interruption; `orElseWith` keeps the original error type.
[`Flow.fold`](/api/Axial.Flow.html#Axial.Flow.fold) branches on success or a typed failure. Defects and interruption
pass through unchanged. Use [`Flow.foldCause`](/api/Axial.Flow.html#Axial.Flow.foldCause) only when the handler must
inspect the full cause, including defects and interruption.

## When error types change

A compound cause is never recovered. When changing error types, each old typed failure becomes a `Cause.Die`
containing an `UnrecoveredFailureException`. Its `Value` retains the original failure as `obj`; the wrapper's message
renders it AOT-safely. The compound structure, existing defects, and interruptions remain.

## When a handler throws

A throwing recovery handler produces a defect, except cancellation with the flow's cancelled token, which interrupts.
