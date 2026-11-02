# Guardrails exemptions — Phase 1

Branch: `chore/guardrails-exempt`. Implementation commit: `a96be7e70`.

Removed the tooling and benchmark path checks from ConcurrencyHazardsAnalyzer. Closed `[exemptions]` table maps AXG009–AXG012 to validated path-glob arrays; matching project paths exempt only host-role projects. TOML explicitly preserves the repository's tooling exemptions (all four rules) and benchmark exemptions (AXG009/AXG012). Runtime, test/capability roles, Cancellation.never and waitUntil/occupyThread symbol handling are unchanged.

Tests-first commit: `3eb4fbadf`. Initial run against unchanged analyzer: 6 failures, 39 passes (three tooling cases, two benchmark cases, source-path scan). Evidence: `artifacts/verify/exempt-red.log`.

Validation:
- Analyzer build: zero warnings/errors (`exempt-build.log`).
- Direct test-project build with `--no-dependencies`: successful. Normal dependency build stops at protected policy mismatch, as anticipated by the brief; no bypass used.
- Focused ConcurrencyHazardsPolicyTests + PolicyTests: 63 passed (`exempt-focused.log`).
- Documentation conventions: passed (`exempt-docs.log`).
- Full analyzer test attempt: timed out after 300 seconds; PackagePropsTests.project reference content invalidates analysis without volatile timestamp invalidation reported failure before timeout. No final suite summary (`exempt-all.log`). Not claimed green.
- No full verify run or result.json yet: explicitly deferred to Phase 2 after lane review merges.

Phase 2: merge origin/main only when coordinator authorizes; run full solution build, investigate any diagnostics without altering enforcement, repeat touched tests 20x, record final fail-first script proof, run detached axial-verify, inspect result.json, run done-check. Human approval must review TOML diff; lock remains untouched in this lane. No push or merge performed.
