# Guardrails: open day to day, reviewed at a point the user chooses

Status: accepted direction (owner, 2026-10-29). Supersedes the build-gate parts of
[guardrails-protected-files.md](guardrails-protected-files.md) (the lock failing the build, approval as authority).

## Why

A local check cannot stop an agent that can compile differently: it can edit a property, an analyzer reference or the
lock itself. Making the lock a gate only adds ceremony. What a human can do is **read what loosened**. So Guardrails
stays open and loud while people work, and puts one cheap review at a point the user picks (a release, a merge into a
protected main, or both). How a repository enforces that point (branch protection, CI, or just a habit) is the
repository's choice. Axial supplies the review, not the enforcement.

## Day to day

- Rules, roles and analyzers run as today. Error rules still fail the build. Guide rules warn.
- Suppressions stay available and active. An agent may add one with a reason (`axial-allow` / `axial-note`) and tell the
  human. It is in control; the human reviews the lot later.
- A policy change (TOML, roles, suppressions, runtime and boundary lists, evaluated project guardrail settings) does not
  fail the build. `axial check` prints one loud warning: "Policy changed since the last review: N changes. Run
  `axial policy review`." and exits 0.

## The review

`axial policy review [--since <ref>] [--json]`

- `<ref>` is a git tag or commit. The default is the **reviewed point** recorded in `Axial.Guardrails.lock`. The first
  review of a repository names one explicitly (Axial: `v0.11.3`, so the first review covers 0.11 to now).
- It compares the policy state at `<ref>` with the working tree and lists only what **loosens or adds exceptions**,
  grouped, a few lines each:
  - suppressions and notes added or whose reason changed (file, text, reason);
  - roles, rule levels, `[runtime]`, boundary, ignore and baseline entries added or relaxed;
  - evaluated project settings: guardrails disabled, role moved, `AxialGuardrail` items removed, `IsTestProject`;
  - changes to the verify script, CI workflows and the analyzer package version;
  - removals are listed as one line each (tightening needs no scrutiny).
- State at `<ref>` comes from git (`git show <ref>:path`). If the lock at `<ref>` predates the suppression inventory,
  the tool scans the sources at that ref with the same suppression parser.
- `axial policy review --check` exits non-zero if anything loosened since the reviewed point. This is the gate a
  repository wires into a release step or a PR job. Without `--check` it only prints.

## Approving

`axial policy approve` means "I read the review". It records the current commit as the **reviewed point** in the lock.
It is a human step by convention, not by mechanism. Nothing pretends it is a security boundary; the docs say so.
`--yes-i-reviewed` stays for a human running it in a script. No TTY requirement, no signing.

## Axial's own release

`dev-docs/ReleaseProcess.md` gains a step before tagging: run `axial policy review`, read it, run `axial policy
approve`, commit the lock. The release workflow runs `axial policy review --check` and refuses to publish if anything
loosened after the reviewed point. First reviewed point: `v0.11.3`.

## What does not change for users

Hard-coded Axial paths leave the analyzer (they move into Axial's own `Axial.Guardrails.toml`); a published analyzer
knows no repository paths. The lock does not try to hash arbitrary files.

## Not doing

Locking whole `.fsproj` files, locking analyzer source, a TTY check, signed approval, bot accounts, GitHub-only
features. Users who want a hard gate use their host's merge rules and run `review --check` in that job.
