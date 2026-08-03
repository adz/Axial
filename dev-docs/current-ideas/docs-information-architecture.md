# Documentation Information Architecture

Status: proposed. Written while the Result split landed and the top navigation became
**Result | Values | Data | Schema | Flow**, with Values grouping Constraint, Refined, and Parse.

The split is structurally correct — the seams are real, the packages are genuinely independent, and the
dependency story is honest. It still reads as a mess. This document says why, compares against how Effect
presents a comparably large surface, and proposes a concrete plan.

## The Diagnosis

**The navigation is a picture of our package graph, not of the reader's problem.**

The current top level asks the reader to already know Axial's taxonomy before they can find anything.
"Values" is the clearest symptom: it is explicitly *not a package and not a namespace*, so it exists purely
as a drawer. Our own `values/_index.md` opens by apologising for itself — "Values is a grouping, not a
package" — which is a reliable signal that a navigation node is an artefact of how we build rather than of
how anyone reads.

Four further symptoms, all downstream of the same cause:

1. **Five peers with no spine.** The landing page presents five equal doors. Nothing tells a newcomer where
   to start, what the through-line is, or which two of the five they will actually need. Equal weight means
   no weight.
2. **Depth is uneven and unexplained.** `result/` is 12 flat pages, `values/` is a grouping over three
   sub-sections plus its own `getting-started` and `tutorials`, `flow/` is 20+ entries with its own
   sub-trees. The same nesting level means very different things in each.
3. **Every product re-teaches onboarding.** Five `getting-started.md`, five `agent.md`, five `llms.txt`,
   several `overview.md` / `what-it-does.md` / `packages-and-platforms.md`. A reader who enters at Schema
   and then needs Constraint starts from zero a second time.
4. **The reader's actual task crosses the boundaries.** "Accept this untrusted input and get a domain value"
   touches Parse, Constraint, Refined, Result, and Schema. Today that is five sections and no single page
   that owns the story. The `values/_index.md` "three in one function" snippet is the best thing in the
   section precisely because it does own a story — there should be many more of those, and they should be
   the entry points, not buried mid-page.

## What Effect Does

Worth being precise, because the lesson is not "be a monolith".

Effect ships as many packages (`effect`, `@effect/platform`, `@effect/sql`, `@effect/ai`, …). Its
documentation almost never mentions that. The nav is ~24 sections named for problems, not artefacts:
Error Management, Requirements Management, Resource Management, Observability, Configuration, Scheduling,
Concurrency, Stream, Testing, Schema, Platform. Packaging appears only where installation forces it.

Three transferable moves:

1. **One unifying mental model, stated on the homepage.** `Effect<Success, Error, Requirements>` is the
   spine everything hangs from. The homepage sells the shape of the type, not the catalogue of modules.
2. **A single, linear Getting Started.** Eleven ordered pages — Introduction, Why Effect?, Installation,
   Importing, The Effect Type, Creating, Running, Generators, Pipelines, Control Flow. One path, one
   sequence, no choose-your-own-adventure. This is the retention mechanism: a reader who finishes it can
   read any other page.
3. **Marketing by problem, docs by concept, packages by necessity.** The homepage headline is literally
   "Stop installing a new package for every problem." We have the opposite constraint — independent
   installability is a genuine selling point for us — but that argues for making packaging *visible where
   it pays off* (a small install matrix, dependency honesty), not for making it the navigation.

The thing we should **not** copy: Effect's flat 24-section sidebar works because everything shares one type.
Ours does not. We need a middle layer Effect does not need.

## Where Axial Genuinely Differs

State this plainly rather than pretending we are Effect:

- Our packages are independently installable and mostly independent of each other. That is a feature and
  the docs should say so early, not bury it in a note.
- We have no single unifying type. `Result` is the closest thing to a shared currency — Constraint,
  Refined, Parse, Schema, and Flow all speak `Result`-shaped outcomes — and that is the spine we have.
- We serve two audiences with different needs: humans onboarding, and LLMs consuming (`agent.md`,
  `llms.txt`). Effect optimises for the first. We should keep both, but stop letting the agent-facing files
  dilute the human path.

## The Proposal

### 1. Give the site a spine: "Untrusted in, proven out"

Adopt one sentence that orders all five products, and make the homepage teach that order rather than
present five doors.

```
raw input  →  Parse  →  Constraint / Refined  →  Schema  →  your domain  →  Flow
                        └──────── every step returns Result ────────┘
```

`Result` is the connective tissue, not a sixth peer. Say that. The homepage should show one end-to-end
snippet that touches four packages in fifteen lines, with each identifier linked to its section. That
snippet is the highest-leverage asset on the site and it does not currently exist.

### 2. Replace `Values` with the three packages, promoted

Delete the grouping node. Top nav becomes:

**Result · Constraint · Refined · Parse · Data · Schema · Flow**

if the nav can hold seven, or group under a *concept* rather than a drawer if it cannot — but never under a
label whose page has to explain that it isn't a thing. A grouping the reader must be taught is worse than
one more item in a list.

If seven is too many for the header, the correct grouping is by **role in the spine**, and the group labels
should be verbs the reader recognises:

- **Admit** — Parse, Constraint, Refined
- **Describe** — Data, Schema
- **Execute** — Flow
- **Result** stays top-level, because it is what all of them return.

That is defensible in a way "Values" is not: it names what the reader is doing, and a reader who has read
the spine sentence already knows where each label sits.

### 3. One Getting Started for the site, not five per product

Create a single `docs/getting-started/` with a linear sequence, Effect-style:

1. What Axial is (the spine sentence, the one snippet)
2. Install what you need (the honest install matrix; independence as a selling point)
3. Everything returns `Result` (the shared currency, ~1 page)
4. Admit a value end to end (Parse → Constraint → Refined, one worked example)
5. Describe a shape (Schema, minimal)
6. Run it (Flow, minimal)
7. Where to go next (routes into the per-product sections)

Then demote each product's `getting-started.md` to a **quickstart** that assumes the site-level path was
read, or drop it where it now only duplicates. Keep exactly one full onboarding path.

### 4. Add task-oriented entry points above the package sections

The pages readers actually search for cross packages. Add a small "How do I…" index — six to ten entries,
each one page, each spanning packages:

- Accept untrusted JSON and get a domain type
- Validate a form and report every error at once (Constraint + accumulating `result { }`)
- Share one validation rule between server and Fable client
- Move an existing FsToolkit codebase across
- Test a schema against fixtures (Data + Schema)
- Add a dependency to an effect without threading it (Flow)

This is where retention is won. A reader who solves their real task in one page stays; a reader who has to
assemble the answer from five sections leaves.

### 5. Normalise the per-product sections

Every product section gets the same four-part shape, in the same order, with the same names:

```
_index.md      — what it is, when you need it, one snippet, install line
concepts/      — the ideas (was: overview, what-it-does, syntax)
how-to/        — task pages (was: the flat sprawl)
reference/     — generated API
```

Today `result/` has 12 flat pages, `schema/` has 20 mixed concept/task/meta pages, `flow/` has its own
scheme. Uniformity is worth more than any individual section's cleverness, because it is what lets a reader
who learned one section navigate the next without re-learning.

Move meta pages (`packages-and-platforms`, `benchmarks`, `aot-trimming-fable`, `comparisons/`,
`fstoolkit-comparison`) out of the learning path into a per-section "Reference & Notes" group or a
site-level Resources section. They serve evaluators, not learners, and they currently sit adjacent to
tutorials at equal weight.

### 6. Keep the agent surface, separate it

`agent.md` and `llms.txt` per product are correct and should stay — but they should not appear in human
navigation. Expose them from a single `/llms.txt` at the site root that indexes the per-product files, and
drop them from the sidebar. Also add a site-level `agent.md` that carries the spine, since an LLM entering
at one package has the same orientation problem a human does.

### 7. Make independence visible, once

One page — linked from the homepage and the getting-started install step — carrying the dependency graph
and the install matrix. `Axial.Refined → Axial.Constraint`, everything else standalone, nothing depends on
`Axial.Result`. Today this fact is repeated in prose in several `_index.md` files and stated authoritatively
nowhere.

## Sequencing

Ordered so each step is independently shippable and none blocks the repository split.

| Phase | Work | Blocks split? |
| --- | --- | --- |
| 1 | Homepage spine + the one end-to-end snippet | No |
| 2 | Site-level `getting-started/`; demote the five product ones | No |
| 3 | Retire the `Values` node; promote Constraint/Refined/Parse (or adopt Admit/Describe/Execute) | No |
| 4 | Normalise all five sections to `concepts/ how-to/ reference/` | No |
| 5 | Add the "How do I…" cross-package index | No |
| 6 | Consolidate agent surface to a root `llms.txt` + `agent.md` | No |
| 7 | Move meta/evaluation pages out of the learning path | No |

Phases 3 and 4 are the ones that touch the most files and should not run concurrently with other docs work.

## Open Questions

1. **Seven top-level items, or three verb groups plus Result?** Both are better than `Values`. The verb
   grouping is stronger if the spine sentence lands on the homepage; the flat seven is safer if it does not.
2. **Does the docs repository stay the assembly point?** The split proposal keeps `Axial` as the docs site
   with reference content generated in the product repositories. A site-level getting-started and a
   cross-package "How do I…" index both live in the umbrella repository and reference all products —
   confirm that is compatible with each product repository building its docs standalone. If it is not, the
   umbrella site becomes the only complete reading experience and the per-repository builds are partial;
   that is an acceptable outcome but should be a decision, not a discovery.
3. **Is `Result` a peer product or the shared currency?** The navigation currently says peer; this document
   argues currency. It cannot be both without confusing readers, and the answer changes the homepage.
