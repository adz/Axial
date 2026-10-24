# Guardrails v2: checked-in policy and signed protected files

## Decision

Guardrails behavior comes only from checked-in files. Replace MSBuild/environment policy switches with one repository file, `Axial.Guardrails.toml`, and one signed approval record, `Axial.Guardrails.approvals`. The build always runs Guardrails when the package is installed. A protected file is accepted only when its current SHA-256 appears in the approval record and that canonical record has a valid signature from a checked-in human key.

Use OpenSSH signatures first: `ssh-keygen -Y sign` works offline, is widely installed, and supports a hardware-backed SSH key whose touch requirement the agent cannot satisfy. Ship an `axial-guardrails approve <paths...>` command which canonicalizes paths, hashes files, updates the record, and invokes the key for one touch-to-sign operation. Verification uses `ssh-keygen -Y verify` semantics in the packaged tool, against `Axial.Guardrails.allowed_signers`.

The minimum model is:

1. rules produce errors and may offer mechanical fixes;
2. a baseline records accepted findings as warnings;
3. explicit files are effect boundaries where reasoned suppressions may occur;
4. one config selects rules and lists boundaries and protected files;
5. one human-signed record approves the exact bytes of protected files.

There is no machine-dependent mode. Local builds, CI, forks, and offline builds evaluate the same files.

## Files and verification

`Axial.Guardrails.toml` contains a format version, enabled rule IDs or rule groups, explicit boundary paths, protected paths, and the baseline path. Package-contributed rules remain possible, but a repository explicitly selects them in this file rather than through mutable MSBuild items. Paths are repository-relative, slash-normalized, case-sensitive identifiers; the verifier resolves and containment-checks them with platform path APIs.

The default protected set is:

- `Axial.Guardrails.toml`, `Axial.Guardrails.baseline`, and `Axial.Guardrails.allowed_signers`;
- `Directory.Build.props`, `Directory.Build.targets`, Guardrails package props/targets, analyzer source, and the approval/check implementation;
- `.github/workflows/**`, `scripts/verify.sh`, and Guardrails check/test scripts;
- every file declared as an effect boundary;
- test inventory/ratchet records; and
- project-added security-sensitive paths.

The config may only add protected paths. The packaged verifier has the mandatory list above built in, preventing a config edit from unprotecting its own trust files. Projects may add exact paths or narrow checked-in globs for workflows and analyzer implementation; boundary declarations remain exact files.

The approval record is deterministic text: format version, signer namespace, sorted `SHA256  path` lines, then one armored SSH signature over the complete canonical payload. Approval removes stale entries and refuses untracked files, symlinks escaping the repository, dirty unrelated protected files, unknown paths, or keys without a hardware-backed/touch-required policy where the platform can attest it. Verification checks canonical form, hashes, signature, allowed principal, and that every required/protected path has an entry. It performs file reads and SHA-256 only: expected warm cost is under 20 ms for normal repositories and under 100 ms for Axial; no Git history walk, network call, project reload, or type-check is added.

The current repo-internal exemption in `Directory.Build.props` compares paths containing `/`; that comparison is Windows-unsafe. V2 removes path-based repo exemptions. Until then it should use normalized platform paths rather than slash spelling.

### The trust boundary is not the repository alone

A repository cannot prove its own verifier ran to an attacker who can edit the verifier, targets, workflow, and commit. Signed hashes prevent an agent from forging human approval, but the agent could delete the check. Therefore:

- local builds run the packaged verifier before analysis and fail closed; this gives fast feedback and catches ordinary mistakes;
- the protected branch requires an **external, non-repository-controlled** status (a small GitHub App or centrally pinned reusable workflow whose configuration the repository token cannot alter) which runs the released verifier and checks the mandatory protected set;
- the ruleset disallows bypass, direct pushes, and ruleset administration by the agent token.

Without that external anchor, this design is tamper-evident rather than tamper-proof. That limitation must be stated rather than hidden behind `CI=true` logic. For a single developer, the GitHub App is small operational overhead but is the only option here that prevents a repository-writing token from approving its own bypass.

## Approval mechanisms considered

| Mechanism | Verdict | Reason |
| --- | --- | --- |
| Signed content-hash record with hardware SSH key | **Use** | Approves exact bytes, works offline and in forks, survives rebases, and requires physical touch. A single signature makes review and verification cheap. |
| Signed commits checked with `git verify-commit` | Fallback | Familiar, but approves an entire commit, becomes awkward across fixups/rebases, requires ancestry/history policy, and does not prove that each protected file's current bytes were reviewed. Merge/squash semantics complicate CI. |
| CODEOWNERS plus branch protection | Defense in depth | Good review UX, but weak for a single developer and local/offline work. A broad `gh` token may approve or merge unless permissions and non-bypass rules are carefully separated. It does not make local builds deterministic. |
| GitHub environment reviewers | Reject for this use | Gates deployments/jobs, not arbitrary protected-file content; no offline story. The agent's web/token permissions determine whether the gate is meaningful. |
| Sigstore/gitsign browser OIDC | Optional fallback | Browser presence can exclude the agent and transparency logs improve auditability, but verification commonly needs network identity/rekor policy and credentials expire. SSH hardware signing is simpler and fully offline. |

If OpenSSH is unavailable, the tool may support minisign as a format-versioned alternative. Signed commits are the migration fallback, not a second simultaneous requirement.

### UX and environments

Normal edits need no action. A protected edit fails with the changed path and the exact `axial-guardrails approve path` command. The human reviews the diff, inserts/touches the key, and commits both file and approval record. The approval record is self-authenticating; it does not need to list its own hash. Changing `allowed_signers` must be signed by a currently allowed key, providing key rotation. Initial trust is established when installing the external check with the initial signer fingerprint.

Offline/local verification and approval both work. CI only verifies and never possesses a signing key. Forks verify upstream approvals unchanged; a fork owner who changes policy must establish a fork trust root and external check, or cannot merge protected changes under upstream policy. Read-only builds of unmodified forks continue to pass.

## Baselines, boundaries, and disabling

Baseline additions and all boundary changes are ordinary protected edits. Human signature replaces the baseline-growth comparison, boundary-count limit, and special governance prose: a human can approve debt or a genuine boundary, while an agent cannot. Keep baseline fingerprint/count validation and stale suppression diagnostics because they detect malformed or obsolete entries, not authorization.

There is no disable property, environment variable, severity property, test mode, or CI mode. To disable Guardrails for a repository, a human approves a config change. To exempt a project or rule, a human approves its explicit config selection. Tests receive explicit selected rules in config rather than `IsTestProject`-driven behavior; the standard template selects test rules for test projects by checked-in path/project mapping.

## Simplification inventory

| Current concept | Action |
| --- | --- |
| Typed analyzers, effect catalog, diagnostics, fixes | Keep |
| Built-in and package-contributed rules | Merge into rules selected by one config |
| `AxialGuardrail` / `AxialGuardrailRule` MSBuild items | Delete as policy inputs; package metadata only discovers available rules |
| `AxialGuardrailsBoundary` items | Merge into config exact paths |
| Suppression comments with rule ID and reason | Keep |
| Suppression integrity/stale checks | Keep |
| Baseline fingerprints and duplicate counts | Keep |
| Candidate baseline generation | Keep as an explicit tool command, not a build property |
| Baseline growth script and its test | Delete; baseline is protected |
| Boundary wildcard rejection and boundary-count limit | Delete; exact paths plus signed review replace both |
| `AxialGuardrailsEnabled`, severity variables, and environment-variable probes | Delete |
| `AxialGuardrailsGenerateBaseline` | Delete; use `axial-guardrails baseline` |
| `AxialGuardrailsInTests`, `IsTestProject` inference | Delete; checked-in config selects behavior |
| `CI=true` bypass rejection and CI-only branches | Delete |
| `nativeAot` setting inferred from MSBuild | Replace with explicit checked-in project selection, or make the rule universally precise |
| Per-build generated rules file and `AXIAL_GUARDRAILS_CONFIG` | Delete; read config directly |
| Baseline file/root/output MSBuild properties | Delete |
| Analyzer bootstrap/path/executable internals | Keep as packaging implementation, not user policy |
| Incremental stamp and analyzer assembly/source inputs | Keep for speed |
| Repository-internal “enable every guardrail” exemption | Delete |
| Protected-file check scripts | Merge into packaged `verify`/`approve` commands |
| Workflow baseline-growth step | Replace with external protected-file status |

## Rules for LLM-generated code

Ranked candidates:

| Rank | Rule | Failure prevented | Precision / fix / expected false positives |
| --- | --- | --- | --- |
| 1 | Discarded effect value | `Flow`, `Task`, `ValueTask`, `Async`, or `Result` is piped to `ignore`, left unbound, or fire-and-forget | Typed analysis is precise; suggest `let!`, `do!`, return, join, or scoped fork, but no universal fix. Low FP; allow explicit boundary suppression. |
| 2 | Swallowed failure | `with _ -> ()`, catch-all returning success, or `Flow.catchAll` discarding cause | Typed AST identifies handlers and result type. No safe fix beyond preserving/rethrowing/cause mapping. Low-medium FP, mostly boundary code. |
| 3 | Shipped placeholder | `NotImplementedException`, TODO `failwith`, and TODO/FIXME markers under production paths | Syntax/text is precise when paths are explicit. Mechanical removal is unsafe. Very low FP. |
| 4 | Blocking or unbounded work in Flow | `.Result`, `.Wait`, `Thread.Sleep`, input-sized unbounded parallel traversal, or retry recursion without schedule/budget | Blocking calls are already precise and should be extended; parallel/budget analysis needs Axial API symbols and conservative data flow. Fix blocking sleep to Clock APIs where mechanical. Low FP for blocking, medium for bounds. |
| 5 | Test integrity ratchet | Fewer discovered tests/assertions, assertion-free tests, skipped tests, or weakened assertion calls | Typed analysis catches assertion-free/skip attributes; a signed inventory records counts per project. “Weakened” semantics are not reliably inferable. No safe fix. Low-medium FP. Inventory decrease is a protected edit. |
| 6 | Unsafe shared mutation | mutable/ref captured and written by parallel branches | Requires closure and concurrency data flow; useful but costly and aliasing causes misses/FPs. No fix. Medium FP. |
| 7 | Library error-channel smell | `exn` or `string` used as public `Flow` error | Signature analysis is precise, but adapters and tiny libraries have legitimate uses. Suggest a domain DU, no automatic fix. Medium FP. |
| 8 | Brittle union wildcard | wildcard match over a project-owned DU | Typed analysis is precise; replace with explicit cases only when generated safely. Medium FP because intentional defaults exist. |
| 9 | Nondeterministic tests | real clock/sleep or waits without timeout in tests | Symbol matching is precise for known calls; boundedness needs local flow analysis. Replace sleeps with test clock when obvious. Low-medium FP. |

Build ranks 1–4 first, then rank 5. They cover the highest-impact agent behaviors—ignored work, hidden failures, placeholders, hangs/resource blowups—and mostly rely on typed symbols rather than naming heuristics. Delay shared-mutation and semantic assertion weakening until data-flow prototypes show acceptable precision. Test deletion itself cannot be inferred reliably from one tree; the signed test inventory makes a decrease visible and human-approved without pretending to understand intent.

## Migration

1. Release the verifier and config schema; generate config and initial approval records with the owner's hardware key.
2. Install the external status/ruleset before deleting existing checks. Run both for one release.
3. Convert boundaries, enabled package guardrails, baseline location, test selection, and AOT selection into config; then delete old knobs/scripts.
4. For **gitkay**, remove its severity pin, migrate its 17 suppressions to rule-specific reasons, classify exact boundaries, baseline remaining adoption debt, and sign config/baseline/boundary hashes.
5. For **LogDug**, remove its severity pin, migrate the two suppressions in `Program.fs` and `Runtime.fs`, declare only genuine boundaries, baseline the rest, and sign.
6. Each downstream repository establishes its own signer identity and external trust anchor; do not copy Axial's authority unless the same human intentionally owns policy for all three.
