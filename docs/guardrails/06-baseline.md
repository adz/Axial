---
title: Adopt Guardrails with a baseline
linkTitle: Adopt with a baseline
description: Record the findings an existing codebase already has, so new code is checked while old code is fixed over time.
---

# Adopt Guardrails with a baseline

Turn Guardrails on in a large codebase and the first build may report hundreds of raw clock reads. You cannot fix
them all before the next merge, and switching the rule off would let new ones in. A baseline records the existing
findings, so the build accepts them and still reports every new one.

## Prepare candidates

```bash
dotnet axial policy baseline --rule effects.ambient
```

This builds the repository's F# projects, collects the current findings for one rule, and adds them to the baseline
file. It works for `raw-effect` (`effects.ambient`) and the other rules up to `escaped-scope`, by name or number
(`AXG001` to `AXG008`). It is safe to run more than once; it keeps existing entries and does not double counts.

A failed build leaves the baseline unchanged, so fix compile errors and unrelated errors first.

## Review and approve

The baseline is policy, so a person reviews it. Read the new entries, run `axial policy review`, then approve the
growth for that rule:

```bash
dotnet axial policy approve --baseline-rule effects.ambient
```

Plain `axial policy approve` refuses baseline growth. `--baseline-rule` allows it only when the rule was newly turned
on for every affected file's role, so a baseline cannot quietly absorb new findings in code that was already checked.

## What an entry matches

```text
AXG001<TAB>src/Legacy.fs<TAB>6ea89f…
AXG001<TAB>src/Duplicates.fs<TAB>218f34… x2
```

The fingerprint covers the rule, the offending code and the declaration it sits in. Moving the code to another line
keeps the entry. Changing the code, or moving it into another declaration, does not, and the finding comes back.
`x2` means two identical findings; a third is reported, with both counts.

Fix baselined findings over time. Removing an entry tightens the policy and needs no review.

## Rules a baseline cannot hold

`raw-concurrency`, `swallowed-exn`, `cancel-none`, `dropped-work` and `guessed-wait` have no baseline. Fix them, or
use the [allowance or note](allowances-and-notes.html) the rule accepts.
