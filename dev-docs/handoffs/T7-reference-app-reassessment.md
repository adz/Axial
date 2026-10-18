# Handoff: T7, reassess deferred Flow work against real applications

For an agent working in a separate checkout of this repository, next to applications this repository cannot see
(for example on a work account). Everything the agent needs is in this file and the files it names.

## Why

`dev-docs/LATER_TODO.md` lists Flow, stream, STM, and runtime work deferred until a concrete application needs it. Axial
should grow from observed need, not from parity with ZIO. Logs Digger and gitkay have already shaped 0.11 (see
`dev-docs/releases/0.11.0.md`); this pass measures the remaining list against more applications before any of it
is built.

## Inputs

- `dev-docs/LATER_TODO.md`: the candidate list. Sections 10 to 14 are the post-1.0 items; earlier unchecked items
  are 1.0 scope.
- `dev-docs/PLAN.md` and `AGENTS.md`: what Axial is for and its design rules.
- `docs/`: the current public API, so a "missing" feature is checked against what already exists.
- The applications: read their source; do not copy their code into this repository.

## What to produce

For each application, one file `dev-docs/reassessment/<app-name>.md`, with no proprietary code or names beyond what
the user approves. Use a short neutral app name if needed. Sections:

1. **Shape**: what the app does in two or three sentences, which Axial packages it uses or would use, its host
   (desktop, service, web, CLI).
2. **Workarounds**: places the app works around a missing Axial feature: hand-rolled loops, locks, raw
   `Task`/`Async` interop, Guardrails suppressions (`axial-allow-effect`), copied helpers. For each: file and a
   one-line description, and the `LATER_TODO.md` item it maps to, or "new".
3. **Needed items**: `LATER_TODO.md` items this app would use now, each with the concrete call site that would
   change and what it would replace.
4. **Not needed**: items the app gives no evidence for.
5. **New gaps**: problems not in `LATER_TODO.md`, with the same file-and-description evidence. Use the label scheme
   from the Logs Digger feedback: verified (ran something), observed (hit while building), read (read the code),
   guess.
6. **Bugs found in Axial**: anything that looks like an Axial defect, with a minimal repro if one can be written
   without the app's code.

Do not change `LATER_TODO.md` or `TASKS.md`; the maintainer merges findings across apps.

## Handing results back

Commit the `dev-docs/reassessment/*.md` files on a branch named `chore/t7-reassessment-<app-name>` in that checkout and
push it, or, when the work account cannot push here, produce a patch (`git format-patch main`) for the user to carry
over. The maintainer session then merges findings into `LATER_TODO.md` (promoting, deferring, or deleting items) and
removes T7 from `TASKS.md`.
