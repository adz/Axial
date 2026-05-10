# FsFlow Tasks

This file is the active queue for `scripts/ralph-loop-tasks.sh`.
Keep completed work out of this file.
Keep settled design decisions in `dev-docs/decisions/`.
Keep live product and architecture direction in `dev-docs/PLAN.md`.

## Phase 5: Documentation Overhaul (Current)

### Core Model & Getting Started
1. [ ] Set `Getting Started` as the main landing page for documentation.
2. [ ] Update `Getting Started` to show how to 'run' a flow and map it to `Task` or `Async`.
3. [ ] Reorganize documentation structure:
    - Create "Managing Dependencies" top-level section.
    - Move Environment Slicing, Capabilities, and Layering under "Managing Dependencies".
    - Focus "Core Model" on Interop, Execution Semantics, and the FsFlow Model.
4. [ ] Overhaul `Task and Async Interop`:
    - Remove mentions of old styles/patterns.
    - Focus on direct binding and the unified model.
5. [ ] Update `Simple Examples` (BASIC_EXAMPLES.md) to use only the current API.

### New Feature Documentation
6. [ ] Add documentation for `Ref` (atomic mutable references).
7. [ ] Add documentation for `Schedule` (retry and repeat logic).
8. [ ] Add documentation for `STM` (Software Transactional Memory).
9. [ ] Add documentation for `Stream` (FlowStream).

### Cleanup & Maintenance
10. [ ] Review and update all existing documentation files for API consistency.
11. [ ] Ensure `_index.md` and sidebar navigation reflect the new structure.
12. [ ] Verify all examples in documentation are up-to-date and compilable.
