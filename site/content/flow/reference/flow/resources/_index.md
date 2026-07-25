---
title: "Resources"
---

This page shows the Flow helpers that register cleanup and manage scoped resources during execution.

- [`Flow.addFinalizer`](./m-flow-flow-addfinalizer.md): Registers an asynchronous finalizer with the current runtime scope.
- [`Flow.addDisposable`](./m-flow-flow-adddisposable.md): Registers a disposable resource with the current runtime scope.
- [`Flow.addAsyncDisposable`](./m-flow-flow-addasyncdisposable.md): Registers an asynchronously disposable resource with the current runtime scope.
- [`Flow.acquireRelease`](./m-flow-flow-acquirerelease.md): Acquires a resource and registers its release with the current runtime scope.
- [`Flow.acquireReleaseWith`](./m-flow-flow-acquirereleasewith.md): Acquires a resource, uses it, and always runs the release action.
