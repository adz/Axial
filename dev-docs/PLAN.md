# FsFlow Plan

This file tracks live product-shape direction and any remaining open questions.
`dev-docs/TASKS.md` is the executable backlog.
`dev-docs/decisions/README.md` indexes the settled decisions that no longer belong here.

## North Star

FsFlow should be framed as one model for Result-based programs in F#.

Start with plain `FSharp.Core.Result` and `Check`, then lift the same logic into `Validation`, `Flow`, `AsyncFlow`, or `TaskFlow` when you need environment, async, task, cancellation, logging, or runtime concerns.

The core progression is:

```text
Check -> Result -> Validation -> Flow -> AsyncFlow -> TaskFlow
```

The same predicate and validation vocabulary should work at every step. The user should not need separate helper worlds for raw checks, fail-fast `Result`, accumulated `Validation`, `Async<Result>`, and `Task<Result>`.

## Settled Decisions

These items are no longer live design questions and are tracked in the decision log:

- [Flow architecture](decisions/flow-architecture.md): workflow family split, namespace continuity, cold/restartable semantics, `ColdTask`, and builder surface
- [TaskFlow and ValueTask](decisions/taskflow-valuetask.md): why `TaskFlow` stays Task-backed and why `ValueTask` stays a boundary shape
- [Validation surface](decisions/validation-surface.md): `Check`, `Diagnostics`, `Validation`, and the applicative `validate {}` split
- [API reference page shape](decisions/reference-page-shape.md): one page per public API surface, side-menu entries, examples, and source links
- [Docs source extraction](decisions/docs-source-extraction.md): source-aware API pages with links back to the implementation
- [Reader-env `yield`](decisions/reader-env-yield.md): `yield _.Field` as shorthand while keeping `Flow.read`
- [Option and ValueOption binding](decisions/option-valueoption-binding.md): keep implicit binding only for `unit` error workflows and use explicit conversion helpers for typed errors
- [Logging ergonomics](decisions/logging-ergonomics.md): keep the core logging abstraction generic and treat `ILogger` as an adapter
- [Validation path scoping](decisions/validation-path-scoping.md): `validate {}` stays root-local while scoped helpers produce `Key` / `Index` / `Name` branches for nested diagnostics

## Live Direction

The remaining product-shape question is how far to normalize the core combinator surface across the
flow families without reintroducing a second helper world, while keeping plain `FSharp.Core.Result`
as the default result story.

- make `ok` / `error` the primary constructors across `Validation`, `Flow`, `AsyncFlow`, and
  `TaskFlow`
- keep `succeed` / `fail` only as aliases where those families already expose them so older code and
  docs still read cleanly during the transition
- add `apply`, `ignore`, and the standard infix operators where the shape fits:
  - `Result`: `<!>`, `<*>`, `>>=`
  - `Validation`: `<!>`, `<*>`, and `>>=` only if we want an explicit monadic shortcut in addition
    to `map2` / `apply` / `and!`
  - `Flow`, `AsyncFlow`, `TaskFlow`: `<!>`, `>>=`, and `<*>` only where it reads naturally beside
    `map2`
- normalize `orElse` and `orElseWith` across the families that support fallback
- keep the `result {}` computation expression as the FsFlow-friendly way to work with
  `FSharp.Core.Result` without adding a parallel helper module
- document `result {}` carefully so it is clear how it composes with standard FSharp.Core
  `Result` values and the `<!>`, `<*>`, and `>>=` operators
- avoid reintroducing separate ad-hoc helper names when the same shape is already covered by the
  existing family combinators and builders

## Done Means

- the docs read like product documentation for the user
- the API reference is useful without opening the source
- every public API is reachable from the side menu
- semantic edge cases are documented and tested
- the project feels like a maintained library, not a design notebook
