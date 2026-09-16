# Scope

A `Scope` owns finalizers for resources and child fibers. Closing it runs registered cleanup in reverse order, at most
once, and waits for cleanup before returning.

Most application code should not construct or mutate `Scope` directly. Use the Flow-level operations:

- `Flow.scoped` creates and closes a child scope.
- `Flow.scopeAcquireRelease` acquires a value into the current scope.
- `Flow.scopeResource` acquires a reusable `Resource` description.
- `Flow.scopeDisposable` and `Flow.scopeAsyncDisposable` register existing .NET resources.
- `Flow.scopeFinalizer` and `Flow.scopeAsyncFinalizer` register custom cleanup.

This runtime lifetime differs from F# `use` and `use!`, which dispose when their lexical computation-expression body
exits. A Flow scope can intentionally span functions, subflows, stream pulls, and child fibers.

Read the [Scopes guide](/scopes/index.html) for a timeline of root, lexical, child, and nested lifetimes before using the
low-level members listed below.
