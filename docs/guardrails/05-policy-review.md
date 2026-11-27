---
title: Policy review
linkTitle: Policy review
description: The lock records the reviewed policy; axial policy review shows what loosened, and a person approves it.
---

# Policy review

A guardrail is only as strong as its policy. One line in `Axial.Guardrails.toml` can turn a rule off, and one
allowance comment can let a raw call through. Those changes should not slip in unnoticed, but they also should not
block every push. Guardrails warns about them and keeps a list for a person to review.

## The lock

`Axial.Guardrails.lock` records the policy as it was when a person last reviewed it: the TOML, the baseline, every
allowance and note, capability claims, analyzer settings and protected build files. It also records the reviewed
commit.

`axial check` compares the repository with the lock:

```text
WARNING: Policy changed since the last review: 3 changes. Run `axial policy review`.
```

Drift is a warning and the exit code is 0. A corrupt lock or an unreadable policy fails. Builds show the same
warning once per build.

The lock makes changes visible. It is not a security boundary: anyone can edit it, and the edit shows in git.

## Review what loosened

```bash
dotnet axial policy review              # since the reviewed commit in the lock
dotnet axial policy review --since v1.2.0
dotnet axial policy review --json
```

The review lists only what lets more code through: new allowances (listed first), relaxed roles or rule levels,
removed capability definitions or `replaces` entries, a capability lowered to `guide`, a project claiming more
files, baseline growth, disabled analysis and changed CI files. Tightening, such as a removed allowance, shows
briefly.

Moving an allowance within its file is not a change. Changing its reason, or moving it to another file, is.

The first review needs `--since <tag-or-commit>`, because the lock has no reviewed commit yet. If that commit has no
policy files, the review reports the policy as introduced.

A project that cannot be evaluated is listed as `cannot evaluate <project>: <reason>`, and `--check` fails until it is
fixed.

## Approve

A person reads the review, then runs:

```bash
dotnet axial policy approve
git commit Axial.Guardrails.lock -m "Review Guardrails policy"
```

Approval records the current commit as reviewed. It checks no identity or signature.

An agent may prepare policy changes and run the review. It must not approve them: there is no flag that lets a tool
stand in for the person reading.

## Gate a release

`axial policy review --check` prints the review and exits non-zero when anything loosened. `axial check --full`
runs it first. Use either at release or merge time.

## Track CI files

A CI workflow can skip the guardrails as easily as the TOML can. List the files that enforce policy, and review shows
their changes:

```toml
[review]
automation = [".github/workflows/**", "scripts/verify.sh"]
```

`axial init` fills this in for your CI host: `.github/workflows/**` for GitHub, `.gitlab-ci.yml` and
`.gitlab/ci/**` for GitLab, `bitbucket-pipelines.yml` for Bitbucket, nothing for Other. Add your own scripts.

## Protected files

These are always part of the lock: the TOML, the baseline, `.editorconfig`, `.globalconfig`, `Directory.Build.*`
and every `.props` and `.targets` file. Test sources are not, unless you protect them.

## Lock format

Version 2 records each allowance by file, normalized comment text and occurrence number, not by line. A version 1
lock is never trusted silently: it needs one reviewed `axial policy approve` to migrate.
