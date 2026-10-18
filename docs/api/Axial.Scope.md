# Scope

A `Scope` owns finalizers for resources and child fibers. Closing it runs registered cleanup in reverse order, at most
once, and waits for cleanup before returning.

Most application code should not construct or mutate `Scope` directly. Use the Flow-level operations:

- [`Flow.scoped`](/api/Axial.Flow.html#Axial.Flow.scoped) creates and closes a child scope.
- [`Flow.scopeAcquireRelease`](/api/Axial.Flow.html#Axial.Flow.scopeAcquireRelease) acquires a value into the current scope.
- [`Flow.scopeResource`](/api/Axial.Flow.html#Axial.Flow.scopeResource) acquires a reusable [`Resource`](/api/Axial.ResourceModule.html) description.
- [`Flow.scopeDisposable`](/api/Axial.Flow.html#Axial.Flow.scopeDisposable) and [`Flow.scopeAsyncDisposable`](/api/Axial.Flow.html#Axial.Flow.scopeAsyncDisposable) register existing .NET resources.
- [`Flow.scopeFinalizer`](/api/Axial.Flow.html#Axial.Flow.scopeFinalizer) registers custom cleanup as a release flow.
- [`Flow.forkResource`](/api/Axial.Flow.html#Axial.Flow.forkResource) acquires a `Resource` in a fiber and registers its release with the current scope.

A forked fiber has its own scope and releases its scoped resources
when it settles. `Flow.zipPar`, `Flow.sequencePar`, and `Flow.traversePar` run their flows in one shared scope;
`Flow.race` gives each branch its own scope, and each timed flow runs in its own scope. Whichever scope holds the
value the caller receives stays open: it moves to the caller's scope and closes with it, so its resources live as if
the caller had acquired them. Any other scope is released — a failed parallel group or a failed race winner closes
before the combinator returns, a timed flow that fails or times out closes before the combinator returns (on timeout,
before the fallback runs), and a race loser closes once it is interrupted, which may be after `race` returns.

This runtime lifetime differs from F# `use` and `use!`, which dispose when their lexical computation-expression body
exits. A Flow scope can span functions, subflows, stream pulls, and child fibers.

Read the [Scopes guide](/scopes/index.html) for a timeline of root, lexical, child, and nested lifetimes before using the
low-level members listed below.

## Scope transfer per combinator

| Combinator | What happens to its scoped resources |
| --- | --- |
| `Flow.zipPar`, `Flow.sequencePar`, `Flow.traversePar`, `Flow.forEachPar` | Move to the caller's scope when every flow succeeds. Released before it returns when any flow fails. |
| `Flow.race` | The winner's move to the caller's scope if it succeeds. The loser's are released once it has been interrupted. |
| `Flow.timeout`, `Flow.timeoutToOk`, `Flow.timeoutWith` | Move to the caller's scope when the flow succeeds in time. Released when it fails or times out. |
| `Flow.fork` and its variants | Stay with the fiber and are released when it settles. `Fiber.join` gives you the value, not the resources. |
| `Flow.forkResource` | The resource is acquired in a fiber and its release goes to the caller's scope. |
| The per-worker resource of `Flow.traverseParUsing` and `Flow.forEachParUsing` | Released when that worker finishes. |
