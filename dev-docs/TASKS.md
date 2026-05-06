# FsFlow Tasks

This file is the active queue for `scripts/ralph-loop-tasks.sh`.
Keep completed work out of this file.
Keep settled design decisions in `dev-docs/decisions/`.
Keep live product and architecture direction in `dev-docs/PLAN.md`.

The numbered items below are intentionally linear so the ralph loop can move through them one at a time.

1. [x] Draft the blog in `dev-docs/func-arch-from-mark-seemann-as-fsflow.md`:
   explain the impure/pure/impure sandwich with FsFlow idioms, show how `result {}` bridges plain
   `FSharp.Core.Result` without reinventing it, document the relationship to `Check` and `Guard`,
   and include the `<!>`, `<*>`, and `>>=` operators in the same story.
2. [ ] Normalize `Validation`:
   `ok` and `error` become the primary constructors; `succeed` and `fail` stay as aliases; add
   `map2`, `map3`, `apply`, `ignore`, `orElse`, and `orElseWith` here; add `<!>` and `<*>`, and only
   add `>>=` if we want an explicit monadic shortcut alongside `map2` / `apply` / `and!`; keep
   `map`, `bind`, `mapError`, `toResult`, `fromResult`, `sequence`, `collect`, `merge`,
   `traverseIndexed`, `at`, `key`, `index`, and `name` here.
3. [ ] Normalize `Flow`, `AsyncFlow`, and `TaskFlow`:
   `ok` and `error` become the primary constructors; `succeed` and `fail` stay as aliases; add
   `map2`, `map3`, `apply`, `ignore`, `orElse`, and `orElseWith` on each family; add `<!>` for
   `map`, `>>=` for `bind`, and `<*>` only where it reads naturally beside `map2`; keep the family
   builders and modules aligned with the same operator story.
4. [ ] Keep `FSharp.Core.Result` as the default result surface:
   do not introduce a parallel FsFlow `Result` helper module; keep the docs and examples pointed at
   standard `Ok` / `Error` usage plus `result {}` for CE-based orchestration.
