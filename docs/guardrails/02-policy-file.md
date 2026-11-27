---
title: The Guardrails policy file
linkTitle: The policy file
description: Roles, rule levels and capability claims in Axial.Guardrails.toml decide where raw calls are allowed.
---

# The policy file

Some code has to make the raw call. The live clock reads `DateTimeOffset.UtcNow`; the program's entry point reads
its arguments and starts the host. Domain code should not: it should ask for the clock, so a test can replace it.
`Axial.Guardrails.toml` says which code is which.

## Roles

Every source file has one role:

| Role | For | Raw effects |
| --- | --- | --- |
| `domain` | Business logic | Reported |
| `services` | Code that combines services | Reported |
| `capabilities` | Implementations of a service | Reported, unless the file claims the capability |
| `host` | Entry points and wiring: `Program.fs`, DI setup | Allowed |
| `test` | Test projects | Warning |

The role comes from, in order: the file's entry under `[files]`, its project's entry under `[projects]`, then
`[default]`:

```toml
version = 2
baseline = "Axial.Guardrails.baseline"

[default]
role = "domain"

[projects]
host = ["src/App.Host/*.fsproj"]
test = ["tests/**/*.fsproj"]

[files]
capabilities = ["src/App/Live/**"]
```

Paths are relative to the repository root. `*` matches within one folder, `**` crosses folders, and `?` matches one
character.

## A capability file may call only what it owns

The `capabilities` role does not allow raw calls by itself. A file gets permission by claiming a capability on its
`Compile` item:

```xml
<Compile Include="Live/LiveClock.fs" AxialCapability="clock" />
```

`LiveClock.fs` may now read the system clock. It still may not read a file or an environment variable: it owns the
clock, not every effect. [Capabilities](capabilities.html) covers claims and defining your own.

## Rule levels

Set how strict the raw-effect rule is per role:

```toml
[roles.domain.rules]
"effects.ambient" = "deny"

[roles.test.rules]
"effects.ambient" = "warn"
```

| Level | Effect |
| --- | --- |
| `allow` | Not reported. |
| `warn` | A warning. |
| `deny` | An error. A [one-call allowance](allowances-and-notes.html) can permit it. |
| `forbid` | An error that nothing can permit, not even a claim or an allowance. |

In the `test` role raw effects are warnings by default, because tests use real effects for setup and checks. Set
`deny` to make them errors there.

Setting a role to `allow` exempts every file in it. Keep that for `host`.

## How a raw call is decided

Each raw call gets one verdict, checked in this order:

1. The role's level is `forbid`: reported.
2. The file claims the capability that owns the call: allowed.
3. The file is `host`, or its role's level is `allow`: allowed.
4. An allowance comment covers the line: allowed.
5. Otherwise: reported at the role's level.

Fast and Full give the same verdict. `axial check` counts the allowed calls per project, so how much is allowed is a
number you can watch.

## Rules in test projects

Test projects run a smaller set of rules by default: `raw-effect`, `stale-allow`, `shared-fixture` and
`guessed-wait`. Tests may start raw tasks, catch everything and drop tokens, so `raw-concurrency`, `swallowed-exn`,
`cancel-none` and `dropped-work` skip them. Set the MSBuild property `AxialGuardrailsInTests` to `all` to run every
rule there too. The project's role in the policy decides this, not `IsTestProject`.

## Exempt a host project from a concurrency rule

A tool or service host may need raw threads or catch-all handlers. List its project under `[exemptions]`:

```toml
[exemptions]
raw-concurrency = ["tools/**/*.fsproj"]
swallowed-exn = ["tools/**/*.fsproj"]
```

Only `host` projects matching the list are exempt, and only from the named rule. The keys are `raw-concurrency`,
`swallowed-exn`, `cancel-none` and `dropped-work`. A pattern that matches no file is an error.

## Other settings

- `[suppressions] ignore = ["tests/AnalyzerFixtures/**"]` keeps fixture sources out of the lock's scan for allowance
  comments. It does not stop the analyzers.
- `[review] automation = [...]` lists the CI files whose changes policy review shows. See
  [Policy review](policy-review.html#track-ci-files).
- Do not set policy through MSBuild. `AxialGuardrailsSeverity` and `AXIAL_GUARDRAILS_SEVERITY` were removed and fail
  the build with `AXIALGR001`. Use rule levels.
- To turn Guardrails off while troubleshooting, pass `-p:AxialGuardrailsEnabled=false` on the command line. A project
  that should never be checked, such as a benchmark, sets `<AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>`
  in its project file. Policy review shows that as a loosening.

An unknown key or a malformed value fails with its reason.
