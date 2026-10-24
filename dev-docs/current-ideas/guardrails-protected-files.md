# Guardrails v2: locked policy, roles, and agent hooks

## Decision

Guardrails v2 uses Rust-like lint policy: the repository declares intent; the analyzer never infers whether code is domain, effects, host, or test code. Policy has three checked-in files:

| File | Purpose |
| --- | --- |
| `Axial.Guardrails.toml` | Default role, project/file role assignments, rule levels, boundaries, baseline path, and protected-file additions |
| `Axial.Guardrails.baseline` | Existing findings accepted for migration or a newly enabled rule; each rule's entries may then only shrink |
| `Axial.Guardrails.lock` | SHA-256 checksums of policy-bearing files, baseline fingerprints, and test-ratchet counts |

Use **TOML**, not JSON. The parser accepts one versioned, closed schema: unknown keys, duplicate declarations, unknown roles/rules, unmatched paths, conflicting same-precedence assignments, and invalid levels fail the build.

There is no signing, approval file, `CI=true` behavior, environment switch, or MSBuild policy switch. Local builds and CI enforce the same files. The lock makes changes conspicuous and gives agent hooks a small protected set; it is not an identity or security mechanism.

## Policy model

### Rule levels

| Level | Build behavior | Source suppression |
| --- | --- | --- |
| `allow` | Rule does not report | Not applicable |
| `warn` | Diagnostic is a warning | Allowed only under the suppression rules below |
| `deny` | Diagnostic fails the build | Allowed only under the suppression rules below |
| `forbid` | Diagnostic fails the build and cannot be suppressed | Never allowed |

`forbid` is terminal: no override, baseline entry, effects declaration, or source comment lowers it. Every effective level is explainable from checked-in config.

### Roles for projects, folders, and files

Roles are **never inferred** from project names, references, output type, test SDK, file names, or directories. The strict fallback is always:

```toml
[default]
role = "domain"
```

Assignments may cover a whole project or file/folder globs within a project. This supports both multi-project systems and the common single-project application. Role precedence is deterministic:

| Precedence | Assignment | Meaning |
| ---: | --- | --- |
| 1 | Matching `[files]` entry | Most specific; overrides the containing project's role |
| 2 | Matching `[projects]` entry | Applies to files in that project not matched by `[files]` |
| 3 | `[default]` | Strict fallback, always `domain` unless explicitly changed |

At the same precedence, overlapping assignments to different roles are an error rather than an ordering rule. Paths are slash-normalized, repository-relative, case-sensitive globs. The tool rejects repository escapes, symlinks escaping the root, and globs that currently match nothing. Explicit globs express influence, not inference: a declared `src/App/Effects/**` controls future files because policy names the folder, not because the analyzer guesses from `Effects`.

A minimal single-project policy has two assignment lines (under the required tables):

```toml
version = 2

[default]
role = "domain"

[files]
effects = ["src/App/Effects/**"]
host = ["src/App/Program.fs"]
```

A multi-project policy can assign broad project roles and carve out files when useful:

```toml
version = 2
baseline = "Axial.Guardrails.baseline"

[default]
role = "domain"

[projects]
effects = ["src/Orders.Sql/Orders.Sql.fsproj"]
host = ["src/Orders.Host/Orders.Host.fsproj"]
test = ["tests/**/*.fsproj"]

[files]
effects = ["src/Orders/Effects/**"]
host = ["src/Orders/Program.fs"]

[roles.domain.rules]
"effects.ambient" = "forbid"
"domain.async-return" = "deny"
"domain.whole-environment" = "deny"
"domain.service-provider" = "deny"

[roles.effects.rules]
"effects.ambient" = "allow"

[roles.test.rules]
"test.integrity" = "deny"
```

A role supplies its rule set; an explicit project/file rule override may make a rule stricter or looser unless the inherited level is `forbid`.

### Domain and effects boundary

| Code role | Allowed | Rejected |
| --- | --- | --- |
| `domain` | Calls explicit services obtained by narrow environment projection | Ambient I/O; typed use of `System.IO`, `System.Net`, or configured DB clients; returning `Task`, `ValueTask`, or `Async`; calls to `Flow.env` or `ServiceProvider.get` |
| `effects` | Live adapters may use ambient effects and reference `System.IO`, `System.Net`, and DB clients | Policy violations unrelated to the declared effect boundary |
| `host` | Composition and startup operations selected by host policy | Domain logic that bypasses explicit services |
| `test` | Test framework, fakes, and configured test operations | Skipped/assertion-free tests covered by the ratchet |

Ambient effects in a declared effects glob need no per-line suppression. Declaring `Effects/**` once means adding `Effects/Stripe.fs` requires no policy edit, while the new live adapter remains visible in the source diff. This is the normal boundary mechanism.

Exact one-off boundaries remain available for exceptional files. They require an exact file declaration and expect-style suppression with a stable rule ID and reason, for example `// axial-allow-AXG001: vendor callback must construct its SDK client here`. The analyzer errors on malformed, unknown, misplaced, or stale suppressions.

### Effect diagnostic ladder

Every ambient-effect diagnostic gives the next actionable design step in this order:

| Step | Condition | Required message |
| ---: | --- | --- |
| 1 | The versioned Axial catalog owns the effect | Name the concrete package and service, for example: `Use IFileSystem from Axial.FileSystem; add it to your env.` |
| 2 | No Axial service exists | `Define a service for this in your env and put its live implementation in an effects file (Effects/...); domain code calls the service.` Link to [`docs/15-notes/03-guardrails.md#defining-an-effect-service`](../../docs/15-notes/03-guardrails.md#defining-an-effect-service), which provides a short how-to and service/live/fake template. |
| 3 | The finding is already in declared effects code | Do not emit the ambient-effect diagnostic and do not require per-line suppression. |

The error is ambient I/O inline in domain code. Its guidance should drive an agent to create or use a service and place the live adapter in a separately visible effects file. `axial new effect <Name>` automates that shape.

## Suppressions, baselines, and lock

The baseline stores rule ID, repository-relative file, stable fingerprint, and duplicate count. A matched finding becomes a warning so debt stays visible. Stale or malformed entries are errors.

| Change | Command and invariant |
| --- | --- |
| Initial v2 adoption | Explicit bootstrap records existing debt; output labels every added rule/count. |
| Enable a rule with existing findings | `axial guardrails baseline --rule <id>` bootstraps **only that newly enabled rule**. It is explicit and noisy; it cannot add findings for established rules. |
| Later baseline update | Entries/fingerprint counts may only shrink for each bootstrapped rule. |
| Test/assertion count increase | `axial guardrails lock` records it normally. |
| Intentional test/assertion count decrease | Only `axial guardrails lock --accept-test-decrease`; the decrease appears prominently in terminal output and the lock summary/diff. |

`Axial.Guardrails.lock` contains a format version, sorted `SHA256  path` records, baseline fingerprint/count sets by rule, and test/assertion counts by project. It has no signature or identity data.

| Mandatory protected input | Why policy-bearing |
| --- | --- |
| TOML, baseline, and lock | Select policy, roles, accepted debt, and ratchets |
| `Directory.Build.props` / `Directory.Build.targets` | Can remove or bypass analyzer execution |
| Repository/project `.editorconfig` and analyzer config | Can change severity or analyzer behavior |
| Project files that build Guardrails tests | Can remove tests or fixtures |
| Additional exact paths declared in TOML | Protect repository-specific enforcement inputs |

The config may add protected paths but not remove the mandatory set. Source effects files are not checksummed: role globs are locked, while normal implementation edits remain normal edits.

Before analysis, `axial guardrails check` hashes the protected set and validates policy, baseline monotonicity, and test counts. A mismatch prints a deterministic summary:

```text
AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED
  rules downgraded:       domain / domain.async-return: deny -> warn
  baseline entries added: 1 (not permitted; use baseline --rule only for a new rule)
  suppressions added:     2
  effects globs added:    src/App/Effects/**
  test count decreased:   3 (requires lock --accept-test-decrease)
  protected files changed: Axial.Guardrails.toml, Orders.Guardrails.Tests.fsproj
Review with a human; then run the applicable axial guardrails command.
```

`axial guardrails lock` parses old/new policy, validates invariants, scans suppressions, counts tests, prints this summary, and confirms interactively (or requires `--accept-policy-change` non-interactively). It refuses baseline growth. It refuses test decreases unless the dedicated `--accept-test-decrease` flag is present; even then the summary labels the accepted decrease loudly. Writes are atomic.

CI runs the identical check and reports suppression and baseline totals by rule/project. There is no CI-only policy.

## One CLI

Guardrails ships as a .NET tool whose executable is `axial`. Grouped commands leave room for application tooling without another executable:

| Command | Purpose |
| --- | --- |
| `axial guardrails check` | Validate protected hashes, config, monotonic baselines, suppressions, and test ratchets |
| `axial guardrails lock` | Review and record allowed policy changes; dedicated flags make exceptional changes loud |
| `axial guardrails baseline --rule <id>` | Bootstrap findings for one newly introduced rule; that rule then only shrinks |
| `axial new effect <Name>` | Generate a service type, live implementation under the configured effects folder, and test fake |
| `axial agents install claude\|codex\|pi\|all` | Install idempotent protected-file hooks and agent guidance |
| Future: `axial new app` | Application template entry point |
| Future: `axial serve` | Persistent analyzer host if startup cost later justifies one |

The NuGet analyzer package still runs during ordinary builds. The tool provides policy workflows, scaffolding, and explicit checks; users do not need a second Guardrails-specific executable.

## Rules, ordered by benefit versus cost

| Rank | Rule | Precision and fix guidance | Cost |
| ---: | --- | --- | --- |
| 1 | Ambient I/O catalog | Typed constructors/members for files, streams, sockets, network, process, clocks, randomness, environment, HTTP, and DB clients. Apply the diagnostic ladder. | One shared symbol walk; low. |
| 2 | Role assembly/type allowlists | Resolved references and symbols enforce domain/effects separation; aliases cannot evade it. | Reuses rank 1. |
| 3 | Discarded computations | Detect discarded `Flow`, `Task`, `ValueTask`, `Async`, or `Result`; suggest `let!`, `do!`, return, join, or scoped fork. | Low. |
| 4 | Domain return/access bans | Detect async carrier returns, `Flow.env`, and `ServiceProvider.get`; suggest `Flow`, `Flow.envWith`, or host wiring. | Low. |
| 5 | Swallowed failures | Restrict to provable catch-all success/ignore shapes and ignored errors. | Low-medium. |
| 6 | Shipped placeholders | Known placeholder expressions and configured TODO/FIXME markers in production roles. | Very low. |
| 7 | Raise through helper from Flow | Bounded same-project call graph, maximum three static edges; no dynamic dispatch. | Medium, bounded. |
| 8 | Construction bypasses | Typed/syntax checks for reflection construction, mutable domain types, and `Unchecked.defaultof`. | Low-medium. |
| 9 | Blocking/unbounded work | Typed blocking checks and conservative unbounded parallel/retry analysis. | Low-medium. |
| 10 | Boolean blindness | Warning for evidence-backed flag clusters; exclude effects/host DTO policy. | Low-medium. |
| 11 | Test integrity ratchet | Known test/assertion symbols, skipped tests, and locked test/assertion counts. | Low; tests only. |

Ranks 1–6 ship first. Rules 7, 9 boundedness, and 10 begin as warnings until corpus measurements establish precision.

## Agent pack

`axial agents install claude|codex|pi|all` merges native configuration, installs a versioned blocker under `.axial/guardrails/`, and reports exact changed files. Re-running is idempotent; `--check` reports drift.

| Agent | Integration |
| --- | --- |
| Claude Code | `PreToolUse` hooks reject edits/writes and obvious shell writes to protected paths. |
| Codex | Supported hooks plus shared blocker; where pre-tool hooks are unavailable, use sandbox deny lists and disclose weaker coverage. |
| pi | Packaged pre-tool-call extension rejects protected edit/write calls and obvious shell writes. |
| All | Stable-marker `AGENTS.md` section says policy files require human review and names the `axial guardrails` workflow. |

Hooks block `axial guardrails lock` and `baseline` by default for agents. Their rejection directs the agent to explain the needed policy change to a human. Hooks are convenience controls, not a security boundary; the build check remains authoritative.

## Current-state transition

| Current item | V2 action |
| --- | --- |
| AXG001 effect catalog | Widen to streams/network/database; split stable IDs where independent policy is useful; add service-first diagnostic ladder. |
| AXG002 suppressions/boundaries | Keep reason/staleness checks; add effects globs for normal adapters and exact files for rare one-offs. |
| AXG003–AXG008 | Keep behavior; route applicability through explicit roles, never inferred project state. |
| Package guardrail groups | Merge into versioned catalog selected by TOML rule IDs; package metadata may discover rules, not set policy. |
| `AxialGuardrailsBoundary` MSBuild items | Replace with TOML effects globs plus exceptional exact boundaries. |
| Baseline generation/growth CI | Replace with `axial guardrails baseline --rule <id>` bootstrap and lock-enforced per-rule shrinkage. |
| Test-count ratchet | Permit intentional decrease only with `lock --accept-test-decrease`, visibly recorded in summary. |
| Severity/test/AOT MSBuild and environment switches | Delete as policy inputs; TOML is the only source. |
| Guardrails-specific CLI names | Replace with grouped commands under the single `axial` .NET tool. |
| Incremental inputs | Keep; include TOML, baseline, lock, protected hashes, role assignments, and catalog version. |

## Delivery and migration

| Phase | Work | Exit criteria / budget |
| --- | --- | --- |
| 1 — policy core | Strict schema, default domain role, project/file precedence, deterministic check/lock, baseline/test exceptions. | Local/CI parity; hashing/parsing under 25 ms typical and 100 ms for Axial. |
| 2 — effects and rules | Effects globs, diagnostic ladder, `new effect`, ranks 1–6 then 7–11. | Positive/negative/role tests; one typed walk; under 15% total analyzer-time increase. |
| 3 — agents and rollout | Agent installers, reports, migrate repositories, remove old switches/scripts/CLI references. | Hooks under 50 ms; offline idempotent installation; old/new checks dual-run for one phase. |

| Repository | Migration steps |
| --- | --- |
| Axial | Set `[default]` to domain; explicitly assign effects/host/test projects and file globs; translate boundaries; bootstrap current baseline; bootstrap each newly introduced rule separately; lock policy/test files; remove old MSBuild/environment policy after dual-run. |
| gitkay | Default to domain; assign effects/host/test regions; convert 17 suppressions to effects globs or reasoned exact one-offs; bootstrap rules individually; install agent hooks. |
| LogDug | Default to domain; explicitly assign `Program.fs` as host and `Runtime.fs` according to actual responsibility; migrate two suppressions; bootstrap rule debt; install hooks. |
| validstate template | Ship default-domain TOML, explicit `Effects/**`, `Program.fs`, and tests, empty baseline, generated lock, Guardrails tests, and agent-install instructions. Generated apps run `axial guardrails lock` once. |

Migration bootstrap is explicit and noisy, not a permanent escape hatch. After a rule is bootstrapped, its baseline only shrinks. Test counts only decrease through the dedicated acceptance flag, and the lock records the decrease prominently.
