---
title: "Flow.Runtime"
weight: 10
---

This page shows the `Flow.Runtime` helpers for closed executor mechanics. These functions expose cancellation, scope ownership, runtime annotations, timeout handling, retry, and resource cleanup. They are intentionally separate from explicit application services such as clock, logging, HTTP, or file-system access.

## Runtime helpers

- [`Flow.Runtime.cancellationToken`](./m-flow-runtime-cancellationtoken.md): Reads the current runtime cancellation token.
- [`Flow.Runtime.catchCancellation`](./m-flow-runtime-catchcancellation.md): Catches <a href="https://learn.microsoft.com/dotnet/api/operationcanceledexception">OperationCanceledException</a> raised by a flow and converts it into a typed error.
- [`Flow.Runtime.ensureNotCanceled`](./m-flow-runtime-ensurenotcanceled.md): Returns a typed error immediately when the runtime token is already canceled.
- [`Flow.Runtime.sleep`](./m-flow-runtime-sleep.md): Suspends the flow for the specified duration, observing cancellation.
- [`Flow.Runtime.scope`](./m-flow-runtime-scope.md): Reads the current runtime scope.
- [`Flow.Runtime.annotations`](./m-flow-runtime-annotations.md): Reads the current runtime annotations.
- [`Flow.Runtime.traceId`](./m-flow-runtime-traceid.md): Reads the current runtime trace id annotation if one is present.
- [`Flow.Runtime.useWithAcquireRelease`](./m-flow-runtime-usewithacquirerelease.md): Acquires a resource, uses it, and always runs the release action.
- [`Flow.Runtime.timeout`](./m-flow-runtime-timeout.md): Fails with the supplied typed error when the flow does not complete before the timeout.
- [`Flow.Runtime.timeoutToOk`](./m-flow-runtime-timeouttook.md): Returns the supplied success value when the flow does not complete before the timeout.
- [`Flow.Runtime.timeoutToError`](./m-flow-runtime-timeouttoerror.md): Alias for <code>timeout</code> that emphasizes typed failure on timeout.
- [`Flow.Runtime.timeoutWith`](./m-flow-runtime-timeoutwith.md): Runs a fallback flow when the source flow does not complete before the timeout.
- [`Flow.Runtime.retry`](./m-flow-runtime-retry.md): Retries typed failures according to the specified policy.
