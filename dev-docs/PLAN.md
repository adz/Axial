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

The next development phase is the CAPS story: explicit capability environments for workflows, delivered
as opt-in packages for the FsFlow platform and common .NET/system effects.

Reference documents:

- [CAPS plan](CAPS_PLAN.md): locked internal design and implementation direction
- [CAPS intended user guide](CAPS_INTENDED_USER_GUIDE.md): target user-facing documentation shape
- [CAPS research](caps-research/README.md): alternatives and discarded approaches

The core direction is:

- add `Needs<'dep>` as the fine-grained dependency contract
- add `Env<'dep>` and `Env<'dep, 'value>` as computation-expression requests
- support direct `let!` / `do!` binding of `Env` requests in `flow {}`, `asyncFlow {}`, and
  `taskFlow {}`
- reuse the existing FsFlow auto-bind/lift behavior for projected `Env<'dep, 'value>` results
- keep named cap-set interfaces as the public composition model
- use default interface implementations so runtime records implement the named cap members, not
  repeated `Needs<'dep>` plumbing
- package capabilities by concern so users only reference the pieces they need
- document flexible environment types such as `TaskFlow<#LoginCaps, LoginError, Session>` as the
  preferred shape for public workflow boundaries that should accept larger runtimes
- keep `IServiceProvider` and manifest-style capability ideas as edge or future tooling stories,
  not the strict core model
- do not make SRTP, anonymous capability intersections, or runtime-only service lookup the primary
  public API

Target package families:

- `FsFlow.Caps.Core`
- `FsFlow.Caps.Context`
- `FsFlow.Caps.Observability`
- `FsFlow.Caps.Observability.MicrosoftLogging`
- `FsFlow.Caps.Observability.OpenTelemetry`
- `FsFlow.Caps.Console`
- `FsFlow.Caps.FileSystem`
- `FsFlow.Caps.Http`
- `FsFlow.Caps.Process`
- `FsFlow.Caps.ServiceProvider`

`dev-docs/TASKS.md` is the executable backlog for this phase and is shaped so
`scripts/ralph-loop-tasks.sh` can complete the work one task and one commit at a time.

## Done Means

- the docs read like product documentation for the user
- the API reference is useful without opening the source
- every public API is reachable from the side menu
- semantic edge cases are documented and tested
- the project feels like a maintained library, not a design notebook
