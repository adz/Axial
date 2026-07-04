---
title: "Flow.Runtime"
weight: 10
---

This page shows the `Flow.Runtime` helpers for closed executor mechanics. These functions expose cancellation, scope ownership, runtime annotations, timeout handling, and retry. User-facing resource combinators such as `Flow.acquireRelease` live on the main `Flow` module; `Flow.Runtime.scope` remains available for advanced code that needs direct scope access.

## Runtime types

- [`Flow.RetryPolicy`](./t-flow-retrypolicy.md):  Defines how runtime retry helpers repeat typed failures in a controlled way.

## Runtime helpers

- [`Flow.Flow.Runtime.cancellationToken`](./m-flow-flow-runtime-cancellationtoken.md): Reads the current runtime cancellation token.
- [`Flow.Flow.Runtime.catchCancellation`](./m-flow-flow-runtime-catchcancellation.md): Catches <a href="https://learn.microsoft.com/dotnet/api/operationcanceledexception">OperationCanceledException</a> raised by a flow and converts it into a typed error.
- [`Flow.Flow.Runtime.ensureNotCanceled`](./m-flow-flow-runtime-ensurenotcanceled.md): Returns a typed error immediately when the runtime token is already canceled.
- [`Flow.Flow.Runtime.sleep`](./m-flow-flow-runtime-sleep.md): Suspends the flow for the specified duration, observing cancellation.
- [`Flow.Flow.Runtime.scope`](./m-flow-flow-runtime-scope.md): Reads the current runtime scope.
- [`Flow.Flow.Runtime.annotations`](./m-flow-flow-runtime-annotations.md): Reads the current runtime annotations.
- [`Flow.Flow.Runtime.traceId`](./m-flow-flow-runtime-traceid.md): Reads the current runtime trace id annotation if one is present.
- [`Flow.Flow.Runtime.timeout`](./m-flow-flow-runtime-timeout.md): Fails with the supplied typed error when the flow does not complete before the timeout.
- [`Flow.Flow.Runtime.timeoutToOk`](./m-flow-flow-runtime-timeouttook.md): Returns the supplied success value when the flow does not complete before the timeout.
- [`Flow.Flow.Runtime.timeoutToError`](./m-flow-flow-runtime-timeouttoerror.md): Alias for <code>timeout</code> that emphasizes typed failure on timeout.
- [`Flow.Flow.Runtime.timeoutWith`](./m-flow-flow-runtime-timeoutwith.md): Runs a fallback flow when the source flow does not complete before the timeout.
- [`Flow.Flow.Runtime.retry`](./m-flow-flow-runtime-retry.md): Retries typed failures according to the specified policy.
