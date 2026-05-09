# FsFlow Tasks

This file is the active queue for `scripts/ralph-loop-tasks.sh`.
Keep completed work out of this file.
Keep settled design decisions in `dev-docs/decisions/`.
Keep live product and architecture direction in `dev-docs/PLAN.md`.

The numbered items below are intentionally linear so the ralph loop can move through them one at a time.

1. [x] Add the core CAPS primitives:
   introduce public `Needs<'dep>`, `Env<'dep>`, and `Env<'dep, 'value>` types in the FsFlow package,
   with XML doc comments and examples that match `dev-docs/CAPS_PLAN.md`; keep the names available
   from the normal `open FsFlow` surface; add focused tests for the primitive shapes and public type
   visibility.
2. [x] Bind whole-dependency `Env<'dep>` requests in `flow {}`, `asyncFlow {}`, and `taskFlow {}`:
   each builder should read the dependency from environments satisfying `Needs<'dep>`, pass it to the
   continuation, preserve existing cold/restartable semantics, and include tests for success,
   missing/wrong environment compile failures where practical, and parity across all three flow
   families.
3. [x] Bind projected `Env<'dep, 'value>` requests in `flow {}`, `asyncFlow {}`, and `taskFlow {}`:
   projection results must reuse the existing direct bind/lift behavior for plain values, `Result`,
   `Flow`, `Async`, `Async<Result<_,_>>`, `Task`, `Task<Result<_,_>>`, `ValueTask`,
   `ValueTask<Result<_,_>>`, `ColdTask`, `ColdTask<Result<_,_>>`, `option` when `error = unit`, and
   `voption` when `error = unit`; cover representative projection tests without duplicating every
   existing builder test.
4. [x] Prove the named cap-set runtime pattern:
   add tests and runnable examples showing fine-grained caps with default `Needs<'dep>`
   implementations, composed use-case caps, larger app runtimes satisfying smaller flows, and small
   test runtimes; include the flexible type style (`TaskFlow<#SomeCaps, _, _>`) where it improves
   public boundary examples.
5. [x] Add the user-facing CAPS guide:
   turn `dev-docs/CAPS_INTENDED_USER_GUIDE.md` into a polished guide under `docs/`, wire it into the
   Docusaurus sidebar, and make the examples compile or stay clearly marked as illustrative when they
   depend on user-defined services; follow `dev-docs/DOCS.md`.
6. [ ] Update generated and agent-facing documentation for CAPS:
   refresh XML docs, generated API reference pages, `docs/AGENT.md`, `llms.txt`, and any guide pages
   that should mention `Env<T>`, `Needs<T>`, cap-set interfaces, and flexible runtime boundaries;
   do not hand-edit generated reference pages as the primary fix.
7. [ ] Validate and polish the full CAPS story:
   run `dotnet test`, `bash scripts/generate-api-docs.sh`, and the Docusaurus build in `site`; fix
   broken links, MDX issues, package/API reference gaps, and stale examples so the implementation,
   docs, generated outputs, and planning docs all agree.
8. [x] Execute Hugo/Docsy documentation migration:
   Implement the migration plan detailed in `dev-docs/DOCSY-PLAN.md` by replacing the Docusaurus
   setup in `site/` with Hugo and Docsy. This includes adding `hugo = "extended"` to `mise.toml`,
   updating the navigation structure to use Hugo `_index.md` and frontmatter instead of `sidebars.js`,
   and modifying `scripts/build-docs-site.sh` to run Hugo. Ensure the default Docsy visual theme is used.

