# Guardrails v2: locked policy, roles, and agent hooks

## Decision

Guardrails v2 uses Rust-like lint policy: the repository declares intent; the analyzer does not infer whether a project is domain code, a host, or an operational boundary. Policy has three checked-in files:

| File | Purpose |
| --- | --- |
| `Axial.Guardrails.toml` | Rule levels, project roles, role rule sets, exact boundary files, baseline path, and protected-file additions |
| `Axial.Guardrails.baseline` | Existing findings accepted only for migration; entries may disappear but never be added |
| `Axial.Guardrails.lock` | SHA-256 checksums of policy-bearing files plus test-ratchet counts |

Use **TOML**, not JSON, for the config. TOML supports comments, readable tables, and small hand-edited diffs without inventing a JSON comment convention. The parser accepts one versioned, closed schema: unknown keys, duplicate declarations, unknown rules, unmatched project paths, and invalid levels fail the build.

There is no signing, approval file, `CI=true` behavior, environment switch, or MSBuild switch. Local builds and CI read the same files and enforce the same policy. An agent can run the lock command; the design does not pretend otherwise. Its purpose is to make policy changes conspicuous, make accidental edits fail immediately, and give agent hooks a simple set of files to block before editing.

## Policy model

### Rule levels

| Level | Build behavior | Source suppression |
| --- | --- | --- |
| `allow` | Rule does not report | Not applicable |
| `warn` | Diagnostic is a warning | Allowed only under the suppression rules below |
| `deny` | Diagnostic fails the build | Allowed only under the suppression rules below |
| `forbid` | Diagnostic fails the build and cannot be suppressed in source | Never allowed |

Precedence is deliberately small: a project receives its role's rule set, then an explicit per-project override may make a rule stricter or looser. `forbid` is terminal: no project override, baseline entry, or source comment lowers it. Every effective level is explainable from the config; project names, references, output type, test SDK, and directory names never infer a role.

### Roles and projects

Typical roles are `domain`, `boundary`, `host`, and `test`, but names are repository-defined. Each project is assigned exactly one role by repository-relative project path. Shared defaults can be expressed once in a role table.

```toml
version = 2
baseline = "Axial.Guardrails.baseline"

[roles.domain.rules]
"effects.ambient" = "forbid"
"domain.async-return" = "deny"
"domain.whole-environment" = "deny"
"domain.service-provider" = "deny"
"domain.boolean-blindness" = "warn"

[roles.boundary.rules]
"effects.ambient" = "deny"
"domain.boolean-blindness" = "warn"

[roles.host.rules]
"effects.ambient" = "deny"

[roles.test.rules]
"effects.ambient" = "warn"
"test.integrity" = "deny"

[projects."src/Orders/Orders.fsproj"]
role = "domain"

[projects."src/Orders.Sql/Orders.Sql.fsproj"]
role = "boundary"
boundary_files = ["Repository.fs"]

[projects."src/App/App.fsproj"]
role = "host"
boundary_files = ["Program.fs"]
```

Paths are slash-normalized, repository-relative, case-sensitive identifiers. Project declarations and boundary entries are exact paths, not globs. The tool resolves paths and rejects repository escapes and symlinks that escape the root.

### Role allowlists

Role checks operate at both project-reference and typed-symbol levels so aliases and fully qualified names behave identically.

| Constraint | Enforcement |
| --- | --- |
| Only `boundary` and `host` projects may reference `System.IO`, `System.Net`, or configured database-client assemblies | Inspect resolved compiler references; report the project file and disallowed assembly. Also inspect typed symbols so framework facade references do not hide usage. |
| `domain` projects may not return `Task`, `ValueTask`, or `Async` from public or internal functions/members | Typed signature analysis, including abbreviations and generic nesting; `Flow` remains the workflow return type. |
| `domain` projects may not call `Flow.env` | Resolved-symbol match. `Flow.envWith` remains the narrow projection API. |
| `domain` projects may not call `ServiceProvider.get` | Resolved-symbol match. Dynamic lookup remains a host-boundary escape hatch. |

The assembly list is explicit and extensible. Initial database entries cover `System.Data.Common`, `System.Data.SqlClient`, `Microsoft.Data.SqlClient`, `Npgsql`, and other configured providers. A domain project cannot evade the rule by using `DbConnection` instead of a concrete provider.

### Suppressions and baseline

Suppression syntax remains expect-style: `// axial-allow-AXG001: live clock implementation` or the rule's future stable ID. Every directive requires an exact rule ID and non-empty reason. It is accepted only in an exact `boundary_files` entry. The analyzer emits an error for malformed, unknown, misplaced, non-boundary, or stale suppressions. `forbid` findings reject suppressions even in boundary files.

The baseline stores canonical rule ID, repository-relative file, stable finding fingerprint, and duplicate count. A baseline finding is downgraded to a warning so debt remains visible. The analyzer errors on stale or malformed entries. `axial-guardrails lock` may preserve or remove baseline entries but refuses any new fingerprint or increased count relative to the existing lock. Initial v2 migration is the one bootstrap operation that records existing debt; after that, the baseline can only shrink.

## Policy lock

`Axial.Guardrails.lock` contains a format version, sorted `SHA256  path` records, the baseline fingerprint/count set, and test counts by project. It is deterministic and has no signature or identity data.

The mandatory protected set is:

| Protected input | Why it is policy-bearing |
| --- | --- |
| `Axial.Guardrails.toml` and `Axial.Guardrails.baseline` | Selects policy, roles, boundaries, suppressible debt |
| `Directory.Build.props` and `Directory.Build.targets` | Can remove or bypass analyzer execution |
| Repository and project `.editorconfig` files | Can change diagnostic severity |
| Analyzer config files (`*.globalconfig`, configured analyzer files) | Can change analyzer behavior or severity |
| Every project file that builds Guardrails tests | Can remove tests or fixtures from the safety net |
| Additional exact paths declared in TOML | Lets a repository protect its own enforcement inputs |

The config can add protected paths but cannot remove the mandatory set. The lock does not checksum source boundary files: adding a boundary or suppression changes TOML or suppression totals and appears in the policy summary, while ordinary implementation edits remain ordinary edits.

Before analyzer execution, the packaged build target hashes the protected set and compares it with the lock. Missing, extra, or changed records fail the build. The failure prints a loud, deterministic summary rather than only “checksum mismatch”:

```text
AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED
  rules downgraded:       domain / domain.async-return: deny -> warn
  baseline entries added: 1 (not permitted)
  suppressions added:     2
  boundaries added:       src/Orders.Sql/Unsafe.fs
  protected files changed: Axial.Guardrails.toml, Orders.Guardrails.Tests.fsproj
Review with a human; then run: axial-guardrails lock
```

`axial-guardrails lock` parses old and new policy, type-checks its invariants, scans suppressions, counts tests, prints the same summary, and asks for explicit confirmation when interactive (or requires `--accept-policy-change` when non-interactive). It **never** accepts baseline growth or a test-count decrease. It then atomically rewrites the lock. Rule downgrades, new boundaries, and new suppressions are permitted only through this explicit command and stay prominent in its output and diff.

CI runs the normal build check and always reports total suppression count, broken down by rule and project. The count is informational; new suppressions are governed by the changed lock and summary, not by CI-only logic.

## Rules, ordered by benefit versus cost

Implement rules in this order. “Typed” means resolved F# symbols or checked signatures, not source-name matching.

| Rank | Rule | Precision and implementation | Fix availability | Expected cost |
| ---: | --- | --- | --- | --- |
| 1 | Ambient I/O catalog expansion | Typed constructor/member matches for path-based `FileStream`, `StreamReader`, `StreamWriter`; `NetworkStream`; `Socket`, `TcpClient`, `UdpClient`, `TcpListener`; `DbConnection` and provider connections including `SqlConnection` and `NpgsqlConnection`. Match inheritance where appropriate. | Suggest the owning explicit service; no general rewrite because dependency plumbing changes signatures. | One symbol walk already shared with AXG001; low. |
| 2 | Role assembly allowlists | Compiler-reference check plus typed usage check for `System.IO`, `System.Net`, and database clients. Exact role comes from config. | Remove the reference or move the adapter to a boundary/host project; no automatic move. | Reference scan is near-zero; symbol check reuses rank 1. |
| 3 | Discarded computations | Typed detection when `Flow`, `Task`, `ValueTask`, `Async`, or `Result` is piped to `ignore`, used as a statement, or otherwise discarded. Exclude explicit ownership APIs. | Contextual suggestions: `let!`, `do!`, return, join, or scoped fork; no universal edit. | Low; local typed expressions. |
| 4 | Domain return and access bans | Typed signatures detect domain functions/members returning `Task`/`ValueTask`/`Async`; resolved calls detect `Flow.env` and `ServiceProvider.get`. | Suggest `Flow`, `Flow.envWith`, or explicit host wiring. Signature conversion is manual. | Low. |
| 5 | Swallowed failures | Typed handler analysis for `with _ -> ()`, catch-all success, ignored `Result.Error`, and `Flow.catchAll` handlers that erase the cause. Restrict to provable success/ignore shapes. | Suggest rethrow, typed failure, or cause mapping; no safe automatic edit. | Low-medium; local control flow. |
| 6 | Shipped placeholders | Syntax plus source trivia for `NotImplementedException`, placeholder `failwith`/`failwithf`, and TODO/FIXME in production-role projects. Avoid ordinary descriptive comments by requiring configured markers or known placeholder expressions. | No safe fix. | Very low. |
| 7 | Raise through helper from Flow | Build a per-project call graph from each `flow {}` body. Follow statically resolved same-project functions/members up to 3 edges; report paths ending in `raise`, `reraise`, `failwith`, `failwithf`, `invalidArg`, or `invalidOp`. Do not chase interfaces, virtual dispatch, function values, or other assemblies. Cache summaries per declaration. | Suggest `Flow.fail`/`Flow.die` at the source; automatic fix only for direct standalone calls already handled by AXG003. | Medium but bounded; no whole-solution fixed point. |
| 8 | Construction bypasses | Typed/syntax checks for `Unchecked.defaultof`, `FSharpValue.Make*`, `[<CLIMutable>]` on domain types, and mutable fields in domain records/classes/unions. Allow compiler/generated code by explicit path, not inference. | Remove bypass, expose a smart constructor, or move serialization DTO to a boundary; no automatic rewrite. | Low-medium. |
| 9 | Blocking and unbounded work | Extend typed blocking checks; detect known unbounded parallel traversal over input-sized collections and retry recursion lacking a `Schedule`, bound, timeout, or decreasing constant. Keep speculative cases at `warn`. | Mechanical replacement for `Thread.Sleep` where context supports `Flow.sleep`; otherwise guidance only. | Low for blocking, medium for bounded local data flow. |
| 10 | Boolean blindness | Warning for domain records with at least two `bool` fields, or several `option` fields used predominantly as presence flags. Require evidence from field types/usages; do not flag configuration/DTO roles. | Suggest a named union with explicit states; no automatic schema rewrite. | Low-medium; one type/use index. |
| 11 | Test integrity ratchet | Detect skipped tests and assertion-free test bodies using known test/assertion symbols. Store discovered test and assertion counts per test project in the lock; lock refuses decreases. | Remove skip/add assertions; count changes are manual. | Low; test projects only. |

Rules 1–6 provide the best precision and feedback per millisecond. Rules 7, 9's boundedness checks, and 10 ship initially as warnings until corpus measurements establish false-positive rates. Test “weakening” beyond known removed assertions is not inferred; the count ratchet exposes a decrease without claiming to understand intent.

## Agent pack

The NuGet package includes templates and `axial-guardrails agents install [claude|codex|pi|all]`. Installation merges tool-native configuration where supported, writes a small versioned hook script under `.axial/guardrails/`, and prints the exact files changed. Re-running is idempotent; `--check` reports drift without editing.

| Agent | Installed integration | Behavior before an edit |
| --- | --- | --- |
| Claude Code | `.claude/settings.json` `PreToolUse` hooks for edit/write and shell commands | Rejects direct edits to protected paths and shell redirections/moves that target them. |
| Codex | Repository-supported command/hook configuration plus the shared blocker script | Rejects protected-path writes before tool execution; where a Codex version lacks pre-tool hooks, install instructions require its sandbox deny list and state that coverage is weaker. |
| pi | A packaged pi extension registering the pre-tool-call hook | Rejects edit/write calls and obvious shell writes to protected paths. |
| All | An `AGENTS.md` section generated between stable markers | Tells agents not to edit policy files, to ask the human, and names `axial-guardrails lock` as a human-reviewed operation. |

The rejection says: `Guardrails policy file <path> is human-controlled. Do not edit or run axial-guardrails lock; explain the required policy change to the human.` Hooks also block invoking `axial-guardrails lock` by default. A human can temporarily disable or bypass an agent hook according to that agent's own mechanism.

Hooks are convenience controls, not a security boundary: shell parsing is incomplete, agents change, and an agent may alter hook configuration. They block common mistakes earlier; the packaged build checksum check remains authoritative.

## Current-state transition

| Current item | Today | V2 action |
| --- | --- | --- |
| AXG001 | Typed ambient-effect catalog: random/secure random, GUID, clocks/time zone, environment, delays/sleep, blocking waits, console, `File`/`Directory`/watcher, process start, WebSocket, `HttpClient`, scope marker | **Keep and widen** with stream, socket/network, and database construction; split stable rule IDs where levels need independent control. |
| AXG002 | Validates rule-specific reasoned suppressions, declared boundaries, placement, and staleness | **Keep**, add `forbid` rejection and TOML exact-boundary source. |
| AXG003 | Detects direct raise/fail helpers inside flow expressions | **Keep and extend** with bounded same-project helper-call analysis. |
| AXG004 | Rejects shared module-level test fixtures | **Keep**, select through the explicit test role rather than inferred test-project state. |
| AXG005 | Detects discarded cancellation tokens in task adapters; currently omitted in tests | **Keep**, make applicability part of role policy rather than `IsTestProject` inference. |
| AXG006 | Detects reflection-dependent formatting for NativeAOT/trimming | **Keep**, remove MSBuild `nativeAot` policy input; configure level by role/project. |
| AXG007 | Detects resources acquired and returned from a settling fiber | **Keep** unchanged apart from common policy/suppression plumbing. |
| AXG008 | Detects scope-owned services returned after a run closes | **Keep** unchanged apart from common policy/suppression plumbing. |
| Built-in effect catalog and package guardrail groups | Rules enabled by `AxialGuardrail`/`AxialGuardrailRule` MSBuild items | **Merge** into the versioned catalog selected by TOML rule IDs and role sets; package metadata may discover rules but cannot set policy. |
| Boundary declarations | `AxialGuardrailsBoundary` MSBuild items; globs can expand | **Merge** into exact TOML project-relative files; delete the MSBuild item as policy. |
| Baseline | Stable fingerprints/counts; matched findings become warnings | **Keep** format semantics; lock bootstrap records the initial set, then growth is impossible. |
| Candidate baseline generation | MSBuild generation mode and output properties | **Replace** with explicit `axial-guardrails baseline` migration command; unavailable after lock bootstrap except to remove stale entries. |
| Baseline growth CI script/test | Compares current baseline with a Git base | **Merge** invariant into lock verification; delete history-dependent script and CI step. |
| Suppressions | Rule ID plus reason, boundary-only; AXG002 checks stale entries | **Keep**, count in lock summary and CI report. |
| Severity/enabled/test/AOT switches | MSBuild properties, generated TSV config, `AXIAL_GUARDRAILS_CONFIG`, and some inferred project state | **Delete** as policy inputs; TOML is the only source. Keep only private packaging paths needed to launch the analyzer. |
| CI checks | Normal builds run analyzer; CI separately checks baseline growth; verification tests grammar and package wiring | **Merge** into identical local/CI lock validation plus analyzer execution; CI additionally prints suppression totals. |
| Incremental analyzer inputs | Targets fingerprint analyzer/config inputs to avoid unnecessary runs | **Keep**, adding TOML, baseline, lock, and protected hashes as deterministic inputs. |

## Delivery plan and performance budget

| Phase | Work | Exit criteria | Added warm-build cost target |
| --- | --- | --- | ---: |
| 1 — core simplification | Add strict TOML schema and roles; add deterministic lock command/check; move levels, boundaries, baseline, test selection, and AOT selection out of MSBuild/environment state; preserve AXG001–AXG008; delete old switches and baseline-growth script after dual-run migration. | Identical policy locally and in CI; lock mismatch summary covers downgrades, baseline/suppression/boundary changes; baseline growth is rejected. | Hashing/parsing under 25 ms typical, under 100 ms for Axial; no extra project type-check. |
| 2 — rules | Implement ranks 1–6, then 7–11. Reuse one typed walk and declaration summary index. Measure warning candidates on Axial and downstream repos before raising defaults. | Every rule has positive/negative tests, role tests, suppression tests, measured false positives, and documented fix guidance. | Ranks 1–6 under 5% analyzer-time increase; all rules under 15%, with no additional compiler invocation. |
| 3 — agent pack and ratchet | Ship Claude/Codex/pi installers and AGENTS section; lock test/assertion counts; CI suppression report; test hook messages and idempotent installation. | Common policy edits are blocked before execution, build still catches bypasses, test decreases fail lock generation, install/check works offline. | Hooks under 50 ms per tool call; count/suppression scans folded into lock/analyzer pass, under 25 ms warm. |

## Migration

| Repository | Migration steps |
| --- | --- |
| Axial | Generate explicit role assignments for every project; translate enabled guardrails and exact boundaries; create the one-time baseline bootstrap; lock mandatory policy/test files; run old and new checks together for one phase; then remove MSBuild/environment policy and baseline-growth CI. |
| gitkay | Remove severity pins; assign domain/boundary/host/test roles; convert its 17 suppressions to exact IDs with reasons and exact boundary files; bootstrap existing findings once; install all applicable agent hooks. |
| LogDug | Remove severity pins; classify `Program.fs` and `Runtime.fs` rather than automatically treating both as boundaries; migrate the two suppressions with reasons; bootstrap remaining debt; install hooks. |
| validstate template | Ship TOML with explicit template project roles, an empty baseline, generated lock, Guardrails test project, agent-pack installation instructions, and no machine-dependent switches. Generated applications replace template paths during creation and run `axial-guardrails lock` once. |

Migration tooling may create the first lock from an existing baseline and test inventory. That bootstrap is explicit and noisy, but it is not a permanent escape hatch. After bootstrap, baseline debt only moves downward and test counts do not move downward; intentional structural policy changes remain possible through the explicit lock command and visible summary.
