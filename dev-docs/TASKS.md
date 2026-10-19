# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T16: Split the HTTP host adapters into their own Axial+Reified product before 1.0
  - Move the two cross-product packages out of the Axial release train once their product boundary is settled.

- [ ] T15b: Migrate gitkay and LogDug to Axial.Files and Axial.ElmishFlows once 0.11 is published
  - Remove gitkay's walk/lines/watch Guardrails suppressions and swap LogDug's Files internals for the package.
  - Replace both apps' flow-to-Elmish bridges with the per-program runtime; replace gitkay's slots and debounce
    with keyed commands and an inbox subscription, and await cleanup on shutdown.
- [ ] T7: Reassess `LATER_TODO.md` Flow work against real applications
  - The remaining reference applications are work applications, reassessed from a separate (work-account) checkout.
    Brief: `dev-docs/handoffs/T7-reference-app-reassessment.md`; findings come back as
    `dev-docs/reassessment/<app>.md` on `chore/t7-reassessment-<app>` branches or patches.
  - Then merge the findings into `LATER_TODO.md` (promote, defer, or delete items) and close this task.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters cross the product boundary here; the reference app lives in [axial-examples](https://github.com/adz/axial-examples).
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
