---
title: "Scope"
weight: 160
type: docs
---

This page shows the `Scope` surface used to own cleanup for resources acquired during provisioning and execution. Scopes register finalizers, disposables, and async disposables, and they close in reverse registration order.

## Core type

- [`Scope`](./t-scope.md):
 Owns finalizers for resources acquired during provisioning or runtime execution.


## Methods

- [`Scope.AddFinalizer`](./m-scope-addfinalizer.md): Registers an asynchronous finalizer to run when the scope closes.
- [`Scope.AddDisposable`](./m-scope-adddisposable.md): Registers a disposable resource for synchronous cleanup.
- [`Scope.AddAsyncDisposable`](./m-scope-addasyncdisposable.md): Registers an asynchronously disposable resource for cleanup.
- [`Scope.AddChild`](./m-scope-addchild.md): Creates a child scope whose cleanup is owned by this scope.
- [`Scope.Close`](./m-scope-close.md): Closes the scope and runs all registered finalizers in reverse order.
