---
title: Build reports
linkTitle: Build reports
description: The report each Guardrails build writes, coverage failures, timings and how results are cached.
---

# Build reports

You need this page when a build says Guardrails could not finish, when it is slow, or when a tool reads its results.

## The report

Each project and target framework writes `axial-guardrails.<TFM>.result.xml` under `obj`. It lists the findings and,
for each rule and file, what happened:

| Entry | Means |
| --- | --- |
| `ran` | The rule checked the file. It may have found nothing. |
| `skipped` | The rule could not check the file. The reason is included. |
| `deferred to full` | A Fast build left this rule to Full. |
| `excluded (generated)` | A file under `obj`, such as `AssemblyInfo.fs`. No rule checks it, because you cannot edit it. Keep your own sources outside `obj`. |

`axial check` reads these reports to print each project's raw-Task count and allowed raw calls. A project with no
report yet shows as "not built yet".

## When Guardrails cannot finish

A clean compile does not prove the rules ran. So Guardrails fails closed:

- **`AXGINF001`** fails the build when analysis did not complete: an analyzer process failed, or coverage is missing.
  The error names the cause, how many checks were skipped and up to three examples. There is no opt-out. Findings
  that were produced are still reported.
- **`AXGINF002`** warns that a source could not be classified because its path is unusable. With an unusable source
  path no rule checks the file; with an unusable project path the file gets the default role. `axial check --full`
  fails on it.

A parse error never counts as complete coverage. An exception in one analyzer leaves the others running.

## Timings

Build with `-p:AxialGuardrailsTimings=true` to write `axial-guardrails.<TFM>.timings.xml` next to the report. It
records, in milliseconds, fingerprinting, start-up, project loading, type checking, each rule and file, and output.
Rules run in parallel, so do not add their times up to get wall time.

## Caching

A result is reused only when every input matches: the tool, compiler arguments, policy, sources and their order, and
the target framework. Full also includes references. A Fast result never satisfies Full. Changing mode reruns the
analysis.

A cache hit replays the findings at their severity. Info findings replay at normal verbosity, so a minimal-verbosity
build shows an Info finding only when it was produced. An unreadable or mismatched report reruns analysis.

Source and policy contents are hashed on every build. Within one build, files whose size and modification time have
not changed are hashed once, and the policy is checked once per policy root. On Linux local filesystems, Guardrails
also checks the inode and kernel change time, so restoring an old timestamp does not hide a change. In a git work tree,
the protected-file list comes from `git ls-files`, so ignored folders are never scanned.

A project built more than once in one build, for example with different `AdditionalProperties`, shows its warnings
once. MSBuild's own summary at the end lists each warning a second time.
