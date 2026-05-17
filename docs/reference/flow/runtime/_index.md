---
title: "Flow.Runtime"
---

This page shows the `Flow.Runtime` helpers for operational concerns. These functions allow workflows to interact with the ambient execution environment for things like logging, time, random numbers, and resource management. They are designed to be used at the boundaries of your application workflows.

## Runtime helpers

- [`Flow.Runtime.cancellationToken`](./m-flow-runtime-cancellationtoken.md): Reads the current runtime cancellation token.
- [`Flow.Runtime.catchCancellation`](./m-flow-runtime-catchcancellation.md): Catches <a href="https://learn.microsoft.com/dotnet/api/operationcanceledexception">OperationCanceledException</a> raised by a flow and converts it into a typed error.
- [`Flow.Runtime.ensureNotCanceled`](./m-flow-runtime-ensurenotcanceled.md): Returns a typed error immediately when the runtime token is already canceled.
- [`Flow.Runtime.sleep`](./m-flow-runtime-sleep.md): Suspends the flow for the specified duration, observing cancellation.
- [`Flow.Runtime.now`](./m-flow-runtime-now.md): Reads the ambient UTC clock owned by the runtime.
- [`Flow.Runtime.log`](./m-flow-runtime-log.md): Writes a message through the ambient runtime logger.
- [`Flow.Runtime.newGuid`](./m-flow-runtime-newguid.md): Creates a new GUID through the ambient runtime GUID generator.
- [`Flow.Runtime.nextInt`](./m-flow-runtime-nextint.md): Creates a random integer through the ambient runtime random generator.
- [`Flow.Runtime.tryGetEnvironmentVariable`](./m-flow-runtime-trygetenvironmentvariable.md): Reads an environment variable from the ambient runtime environment provider.
- [`Flow.Runtime.useWithAcquireRelease`](./m-flow-runtime-usewithacquirerelease.md): Acquires a resource, uses it, and always runs the release action.
- [`Flow.Runtime.timeout`](./m-flow-runtime-timeout.md): Fails with the supplied typed error when the flow does not complete before the timeout.
- [`Flow.Runtime.timeoutToOk`](./m-flow-runtime-timeouttook.md): Returns the supplied success value when the flow does not complete before the timeout.
- [`Flow.Runtime.timeoutToError`](./m-flow-runtime-timeouttoerror.md): Alias for <code>timeout</code> that emphasizes typed failure on timeout.
- [`Flow.Runtime.timeoutWith`](./m-flow-runtime-timeoutwith.md): Runs a fallback flow when the source flow does not complete before the timeout.
- [`Flow.Runtime.retry`](./m-flow-runtime-retry.md): Retries typed failures according to the specified policy.

