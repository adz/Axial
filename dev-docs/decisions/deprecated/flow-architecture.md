# Flow Architecture

Status: decided.
Recorded: 2026-04-27 to 2026-04-28.

## Extracted From

- `dev-docs/PLAN.md`:
  - `Current Direction`
  - `Package Boundary`
  - `Workflow Semantics`
  - `ColdTask`
  - `Bind Surface`
  - `Extension-Member Confirmation`

## Source Dates

- 2026-04-27: `Confirm asyncFlow extension-member approach`
- 2026-04-28: `Decide TaskFlow stays Task-backed`
- 2026-04-28: `Confirm no separate valueTaskFlow abstraction`

## Decision

Use three workflow types:

- `Flow<'env,'error,'value>` for sync/result-oriented work.
- `AsyncFlow<'env,'error,'value>` for async workflows in core `Axial`.
- `TaskFlow<'env,'error,'value>` for .NET task-based workflows in the main `Axial` package.

Keep the core package unified:

- `Axial` exports `Flow`, `AsyncFlow`, `TaskFlow`, `ColdTask<'value>`, and the sync/async/task combinators.
- the task-specific types and helpers live in `Axial`.

Keep workflows cold and restartable:

- reruns start from scratch for `Flow`, `AsyncFlow`, and `TaskFlow`
- hot `Task` and `ValueTask` inputs are interop shapes, not the semantic identity of the workflow
- `ColdTask<'value>` is the deferred `CancellationToken -> Task<'value>` shape when rerun fidelity matters

## Consequences

- Sync flows do not carry cancellation/runtime concerns in their representation.
- Task-oriented concepts are part of the main public contract.
- the task surface extends `asyncFlow {}` using a prioritized overload strategy:
  - `Task<Result<'T, 'E>>` and `ValueTask<Result<'T, 'E>>` are intrinsic members of `AsyncFlowBuilder` to ensure they take priority over plain `Task<'T>` overloads during member resolution.
  - Plain `Task`, `Task<'T>`, and `ValueTask` overloads remain as extension members in an auto-open module.

## Notes

- `ColdTask<'value>` is nominal, not a type alias.
- `ColdTask<Result<'value,'error>>` is the typed-failure cold-task shape.
- `ColdTask` can ignore cancellation and still be valid as long as it remains deferred and restartable.
