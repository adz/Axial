---
title: Flow
api:
  family: Axial.Flow
  sections:
    - id: create
      title: Create
      order: 10
      members: ["ok", "succeed", "error", "fail", "die", "fromResult", "fromOption", "fromValueOption", "verify", "delay", "never", "ofExit"]
      facets:
        task: [create]
    - id: transform
      title: Transform
      order: 20
      members: ["map", "ignore", "bind", "(>>=)", "tap", "tapError", "mapBoth"]
      facets:
        task: [transform]
    - id: combine
      title: Combine and sequence
      order: 30
      members: ["zip", "map2", "apply", "map3", "(<!>)", "(<*>)", "traverse", "sequence", "traversePar", "traverseParUsing", "forEachPar", "forEachParUsing"]
      facets:
        task: [combine]
    - id: errors
      title: Errors and recovery
      order: 40
      members: ["mapError", "widenError", "tracedError", "fold", "foldCause", "catch", "orElseFlow", "orElseWith", "recover", "orElse", "catchCancellation"]
      facets:
        task: [errors]
    - id: concurrency
      title: Run concurrently
      order: 50
      members: ["supervise", "fork", "forkNamed", "forkGraceful", "forkDetached", "forkResource", "memoize", "forkReplacing", "forkReplacingKey", "zipPar", "sequencePar", "sequenceParWith", "race", "runOn"]
      facets:
        task: [concurrency]
    - id: resources
      title: Manage resources
      order: 60
      members: ["scopeDisposable", "scopeAsyncDisposable", "scopeResource", "scopeAcquireRelease", "scopeFinalizer", "scoped", "onExit", "ensuring", "onInterrupt"]
      facets:
        task: [resources]
    - id: environment-time
      title: Environment and time
      order: 70
      members: ["env", "envWith", "localEnv", "scope", "annotations", "traceId", "fiberId", "cancellationToken", "ensureNotCanceled", "sleep", "timeout", "timeoutToOk", "timeoutToError", "timeoutWith", "retry", "repeat", "withFiberObserver", "addFiberObserver", "withFiberRegistry", "withAnnotationSink", "addAnnotationSink", "annotate", "withTraceId"]
      facets:
        task: [environment-time]
    - id: run-interop
      title: Run and interop
      order: 80
      members: ["RunSynchronously", "StartAsTask", "StartAsValueTask", "ToAsync", "fromAsync", "fromAsyncResult", "attemptAsync", "fromBlocking", "fromBlockingResult", "attemptBlocking", "fromTask", "fromTaskResult", "attemptTask", "fromValueTask", "fromValueTaskResult", "attemptValueTask", "awaitStartedTask", "awaitStartedTaskResult", "attemptStartedTask", "awaitStartedValueTask", "awaitStartedValueTaskResult", "attemptStartedValueTask", "toAsync", "startTask", "run"]
      facets:
        task: [run-interop]
---
# Flow
