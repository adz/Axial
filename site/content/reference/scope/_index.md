---
title: "Scope"
weight: 160
type: docs
---

This page shows the `Scope` surface used to own cleanup for resources acquired during provisioning and execution. Scopes register finalizers, disposables, and async disposables, and they close in reverse registration order.

## Core type

- [`Flow.Scope`](./t-flow-scope.md):
 Owns finalizers for resources acquired during provisioning or runtime execution.


## Methods

- [`Flow.Scope.AddFinalizer`](./m-flow-scope-addfinalizer.md):
- [`Flow.Scope.AddDisposable`](./m-flow-scope-adddisposable.md):
- [`Flow.Scope.AddAsyncDisposable`](./m-flow-scope-addasyncdisposable.md):
- [`Flow.Scope.AddChild`](./m-flow-scope-addchild.md):
- [`Flow.Scope.Close`](./m-flow-scope-close.md):
