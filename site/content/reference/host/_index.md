---
title: "HostContext"
type: docs
---

The `HostContext` type and module split host services from application dependencies and carry the cancellation token for task-based execution. In the current foundation it is the execution carrier above the adapter layer, not the host storage engine.

## Core type

- [`HostContext`](./t-hostcontext-2.md): 
 Captures the two-context shape of a task workflow execution:
 host services, application capabilities, and the cancellation token for the current run.
 

## Module functions

- [`HostContext.create`](./m-hostcontext-create.md): Creates a host context from the supplied host services, app environment, and cancellation token.
- [`HostContext.host`](./m-hostcontext-host.md): Reads the host half of a host context.
- [`HostContext.appEnv`](./m-hostcontext-appenv.md): Reads the application environment half of a host context.
- [`HostContext.cancellationToken`](./m-hostcontext-cancellationtoken.md): Reads the cancellation token stored in a host context.
- [`HostContext.mapHost`](./m-hostcontext-maphost.md): Maps the host half of a host context.
- [`HostContext.mapAppEnv`](./m-hostcontext-mapappenv.md): Maps the application environment half of a host context.
- [`HostContext.withHost`](./m-hostcontext-withhost.md): Replaces the host half of a host context.
- [`HostContext.withAppEnv`](./m-hostcontext-withappenv.md): Replaces the application environment half of a host context.

