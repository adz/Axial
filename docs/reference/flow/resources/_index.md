---
title: "Resources"
---

This page shows the Flow helpers that register cleanup and manage scoped resources during execution.

- [`Flow.addFinalizer`](./m-flow-addfinalizer.md): Registers an asynchronous finalizer with the current runtime scope.
- [`Flow.addDisposable`](./m-flow-adddisposable.md): Registers a disposable resource with the current runtime scope.
- [`Flow.addAsyncDisposable`](./m-flow-addasyncdisposable.md): Registers an asynchronously disposable resource with the current runtime scope.
- [`Flow.acquireRelease`](./m-flow-acquirerelease.md): Acquires a resource and registers its release with the current runtime scope.
- [`Flow.acquireReleaseWith`](./m-flow-acquirereleasewith.md): Acquires a resource, uses it, and always runs the release action.
