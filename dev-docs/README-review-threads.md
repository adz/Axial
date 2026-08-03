# Review threads

Multi-agent design reviews live in one folder per reviewer, one file per round:

```
dev-docs/<reviewer>/<YYYY-MM-DD>-round-<N>.md
```

Rules:

- **Never append to a file someone else has already read.** A round is immutable once written; the reply is a
  new file in the replier's own folder. This keeps each read bounded instead of re-reading a file that grows
  every exchange.
- Open each round with a one-line pointer to the file it answers, and (once answered) to the file that
  answered it.
- Round numbers are per reviewer and monotonic; the date is the day it was written.

## Active threads

### constraint-unification (`dev-docs/current-ideas/constraint-unification.md`)

| round | file | summary |
|---|---|---|
| Bob 1 | `bob/2026-08-01-round-1.md` | 9 findings + 3 open questions |
| Kevin 1 | `kevin/2026-08-01-round-1.md` | disproves Bob's item 1; adds findings A–D and a proposed shared conclusion |
| Bob 2 | `bob/2026-08-01-round-2.md` | concedes item 1; De Morgan fix for negation; extends finding C; new finding E |
| Bob 3 | `bob/2026-08-01-round-3.md` | verification pass: downgrades E, adds F (contramap regression) and G (flat→tree migration is the bulk of the work) |
| Kevin 2 | `kevin/2026-08-01-round-2.md` | narrows the negation table; agrees non-empty `any`, no diagnostic rewriting, contextual inspection; argues terms' placeholder cases must go too |
| Bob 4 | `bob/2026-08-01-round-4.md` | settles Presence from source; accepts the partial-order and boundary objections; withdraws "reserved cases cost nothing"; drafts the joint findings list |

| Kevin 3 | `kevin/2026-08-01T17-45-00-round-3.md` | agrees F and G; proposes opaque `notWith` only, contextual lowering, and a concrete initial surface |
| Bob 5 | `bob/2026-08-01T21-55-26-round-5.md` | accepts opaque `notWith` (no `Check.not` exists today); corrects the Presence record; **joint recommendation drafted** |

| Kevin 4 | `kevin/2026-08-01T18-05-00-round-4.md` | signs findings 1 and 3–16; accepts the Presence correction; presses the API-capability argument against a partial `not` |
| Bob 6 | `bob/2026-08-01T21-58-00-round-6.md` | no disagreement remains; artifact published |

| Kevin 5 | `kevin/2026-08-01T21-57-11-round-5.md` | completion audit: accepts the recommendation, requires four corrections |
| Bob 7 | `bob/2026-08-01T22-02-00-round-7.md` | four corrections applied |
| Kevin 6 | `kevin/2026-08-01T22-05-00-round-6.md` | crossed round 7 in flight; new finding: the governing principle contradicts the trusted JSON codec contract |
| Bob 8 | `bob/2026-08-01T22-08-00-round-8.md` | §2.15 added dividing interpreters by claim rather than by value production; §2.7 narrowed |
| Bob 9 | `bob/2026-08-01T22-14-00-round-9.md` | Adam settled all four escalated decisions; §4 is now requirements |
| Bob 10 | `bob/2026-08-01T22-22-00-round-10.md` | correction: `Check.not` does exist (backtick-declared); §2.2 reason 1 rewritten, conclusion unchanged |
| Bob 11 | `bob/2026-08-01T22-40-00-round-11.md` | records the sentinel/escape justification for `any`; corrects the slice-3 difficulty estimate |
| Kevin 7 | `kevin/2026-08-01T22-04-47-round-7.md` | **signs the joint review** |
| Kevin 8 | `kevin/2026-08-01T22-28-44-round-8.md` | agrees both additions; catches that a partially-lowered `Any` is stricter than runtime |
| Bob 12 | `bob/2026-08-01T22-50-00-round-12.md` | additions folded in; opens the Reason/Atom duplication spike |
| Bob 13 | `bob/2026-08-01T23-00-00-round-13.md` | worked design for Violation-as-failed-expression; `constraintCodeFor` evidence settles it |
| Kevin 9 | `kevin/2026-08-01T22-35-15-round-9.md` | accepts removing `Reason`; proposes the narrower `AtomicViolation` payload |
| Kevin 10 | `kevin/2026-08-01T22-37-27-round-10.md` | rejects embedding `ConstraintInspection`, with four concrete objections |
| Bob 14 | `bob/2026-08-01T23-12-00-round-14.md` | concedes the payload; §2.10 folded in with two amendments |
| Kevin 11 | `kevin/2026-08-01T22-42-48-round-11.md` | typed `UnsupportedOperand` instead of generated prose; reuse in `OpacityReason` |
| Bob 15 | `bob/2026-08-01T23-20-00-round-15.md` | folded in; drops `OneOf` from the catalogue (always portable) |
| Kevin 12 | `kevin/2026-08-01T22-48-07-round-12.md` | reuse `RelationOperator`; corrects the source citation |
| Bob 16 | `bob/2026-08-01T23-35-00-round-16.md` | corrections folded; collapses `OpaqueViolation`; adds §2.15 naming congruence |
| Kevin 13 | `kevin/2026-08-01T22-55-01-round-13.md` | `ConstraintDescription`; separates format annotation from enforcement |
| Bob 17 | `bob/2026-08-01T23-50-00-round-17.md` | all four folded; adds the `Trimmed` boundary to pattern lowering |
| Bob 18 | `bob/2026-08-02T00-05-00-round-18.md` | clean-slate pass: contradictory keys, `describe`/`render` collision, the nested-type test, All/Any invariant |
| Kevin CS1 | `kevin/2026-08-01T23-01-05-clean-slate-1.md` | independent clean-slate pass: five findings on opacity prose, portable values, regex dialect, text length, `Format.Named` |
| Bob 19 | `bob/2026-08-02T00-25-00-round-19.md` | all five verified by execution and folded as §3; `Numeric` lowering invalidated |
| Kevin CS3 | `kevin/2026-08-02T00-31-00-clean-slate-3.md` | non-unary failure groups; the concrete `ConstraintValue` algebra |
