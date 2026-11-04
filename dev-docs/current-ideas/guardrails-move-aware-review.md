# Move-aware policy review (backlog, not implemented)

Deferred. Recorded so it is not rediscovered as a bug.

## Problem

`axial policy review` and the lock identify policy items by repository path: `[files]`/`[projects]` assignments,
`[boundaries].files`, `[runtime].files`, `[exemptions]`, evaluated project settings and suppression/note records
(by file, comment text and occurrence). Moving or renaming a file or project therefore shows its allowances, notes and
settings as *removed* at the old path and *added* (loosened) at the new path, even when nothing changed. A pure
refactor then needs a human review of items that are not new decisions, which trains reviewers to approve noise.

## Idea

Match items by content before falling back to paths:

- Suppressions and notes: pair a removed and an added record with the same rule, normalized comment text and
  surrounding declaration fingerprint (the baseline fingerprint already normalizes the enclosing declaration).
- Boundary/runtime/file-role entries: pair a removed and an added path when git reports a rename (`git diff -M`) or
  the file content hash matches.
- Project settings: pair by project content hash or git rename; compare evaluated settings, not paths.
- Report paired items as "moved" (informational), never as loosening; anything unpaired keeps today's behaviour.

## Constraints

- Tamper evidence must hold: a move combined with a reason change, rule change or an extra occurrence is still a
  review item. Pairing is one-to-one and must not let one removal cover several additions.
- No heuristics that silently approve: when pairing is ambiguous, report both sides as today.
- The lock format may need a content key per record; changing it is a lock-format version bump with a reviewed
  migration, like version 1 to 2.
