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

- [`Scope.AddFinalizer`](./m-scope-addfinalizer.md):
- [`Scope.AddDisposable`](./m-scope-adddisposable.md):
- [`Scope.AddAsyncDisposable`](./m-scope-addasyncdisposable.md):
- [`Scope.AddChild`](./m-scope-addchild.md):
- [`Scope.Close`](./m-scope-close.md):
