---
title: Fast and Full checks
linkTitle: Fast and Full
description: Fast gives quick advice on changed files in every build; Full runs every typed rule for CI and releases.
---

# Fast and Full checks

A check that adds seconds to every build gets turned off. A check that only reads syntax misses things. Guardrails
has both, and you choose by build:

| | Fast | Full |
| --- | --- | --- |
| Runs | Every build, by default | `-p:AxialGuardrailsMode=Full`, or `axial check --full` |
| Reads | Syntax of changed files, in the build process | Every file, with full type information |
| Rules | The rules syntax can decide | Every rule |
| Fails the build on guidance | Never | Errors fail; see [checkpoints](allowances-and-notes.html#checkpoints) |

```bash
dotnet build                                 # Fast
dotnet build -p:AxialGuardrailsMode=Full     # Full
dotnet axial check --full                    # Full, every project, plus the policy review gate
```

The value is case-sensitive: `fast` or `FULL` fails. Nothing else picks the mode: not `CI`, not the Release
configuration, not the branch.

## What Fast can miss

Fast sees names, not types. It knows `DateTime.UtcNow` after `open System`, but not that `t` in `t.Result` is a task.
So:

- It skips names that are shadowed or aliased anywhere in the file.
- It skips short names whose namespace the file never opens.
- It ignores inactive `#if` branches.
- Rules that need types run only in Full.

When Fast is not sure, it marks the finding `[fast/unverified]` and reports it as Info. It never blocks a build on
that. No Fast findings does not mean Full passes.

| Rule | Fast | Full adds |
| --- | --- | --- |
| `raw-effect` | Catalog names that resolve through the file's `open`s, and `.GetAwaiter().GetResult()` chains | Resolved symbols, aliases, overloads, the completed-task exceptions |
| `raise-in-flow` | `raise`/`failwith` directly in `flow { }` | The error type, and a typed fix |
| `shared-fixture` | Same shapes as Full, as a Warning | Policy severity |
| `reflection-format` | Literal `%A` formats | Interpolation, `ToString` and boxed values by type |
| `raw-concurrency` | Qualified constructor and API names | Resolved identity |
| `guessed-wait` | `Thread.Sleep`, and `Task.Delay`/`Async.Sleep` awaited by `do!`/`let!` | Resolved symbols, token overloads, timeout constants |
| `task-surface` | `task`/`backgroundTask` by name, annotated `Task`/`ValueTask` return types | Aliased builders, type abbreviations, inferred return types |
| Others | Not run | Run |

## Full in CI

Run `axial check --full` in CI and before a release. It:

1. Fails if [policy review](policy-review.html) shows unreviewed loosening.
2. Builds the one `.slnx` or `.sln` in the current folder in Full mode, as a checkpoint. The configuration defaults
   to Release; change it with `--configuration <name>`.
3. Checks every F# project and target framework. A project without Guardrails fails, unless its project file sets
   `<AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>`.
4. Fails on Error findings, a source it could not check (`AXGINF002`) or missing coverage.

Warnings, hints and baselined findings keep their severity. Build output goes to stderr, so `--json` output stays
parseable.

## Visual Studio

Visual Studio's MSBuild cannot load Fast's parser. Asking for Fast there runs Full out of process instead, and says
so. Its errors are lowered to warnings, so a Visual Studio build fails no more often than `dotnet build`, except at a
checkpoint. Use `dotnet build` for Fast feedback.

## Unsupported flags

Fast parses with the project's `DefineConstants`, `LangVersion`, `OutputType` and `OtherFlags`. A response file in
`OtherFlags` fails with `AXGINF001`, because Fast cannot see the defines inside it. Pass the flags directly, or use
Full.
