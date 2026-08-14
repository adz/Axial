# Repository Instructions

This file is for agent instructions, not user-facing documentation.

Keep a strict split between:

- agent instructions for contributors and coding agents
- user-facing documentation for library users

Do not put agent guidance in `README.md` or under `docs/`.

When writing or editing user-facing docs, follow the documentation guide in [`dev-docs/DOCS.md`](dev-docs/DOCS.md).

Before broad repository search, read [`dev-docs/AGENT_INDEX.md`](dev-docs/AGENT_INDEX.md) for the compact maintainer
map, generated-path rules, and task routing.

Refer to [`dev-docs/PLAN.md`](dev-docs/PLAN.md) for architectural direction and
[`dev-docs/TASKS.md`](dev-docs/TASKS.md) for the active queue.

## Architecture Invariants

### EFFECT BOUNDARY — DO NOT HIDE AMBIENT EFFECTS IN SERVICE ADAPTERS

- `Axial.Process` and `Axial.HttpClient` may perform only the effect named by their core, mockable service type (`IProcess` or `IHttp`). Any additional effect must be an explicit, mockable dependency visible in the implementation signature.
- In particular, never call `DateTimeOffset.UtcNow`, `DateTime.Now`, or another ambient clock from Process or Http. Inject `Axial.PlatformService.IClock` into live implementations and use `clock.UtcNow()`.
- Apply the same rule to randomness, GUID generation, environment variables, filesystem, console, and other operational effects: use the appropriate explicit service from `Axial.PlatformService` or another package whose core type is present in the signature.
- `src/Axial.Guardrails` is an FSharp.Analyzers.SDK analyzer (`fsharp-analyzers` CLI) that checks this rule automatically across `src/Axial*`. `bash scripts/run-guardrails.sh` runs it and is wired into CI; run it before treating an effect-boundary change as done, and see `docs/15-notes/03-guardrails.md`. A finding at a genuine boundary (a `live` service implementation, a process entry point) is marked with `// axial-allow-effect: <category>` or a file-header `// axial-allow-effect-file: <category>`, never silenced by disabling the analyzer. `scripts/guardrails-exclude.txt` opts a whole project out when it genuinely cannot be checked; `AXIAL_GUARDRAILS_SEVERITY=warning` downgrades findings to non-failing during a migration. Neither is a substitute for reviewing the finding.

- `Flow<'env, 'error, 'value>` is the public workflow model. Do not reintroduce public `Effect`, `EffectFlow`, `AsyncFlow`, `TaskFlow`, or carrier-specific workflow concepts.
- Core `Axial` and its operational packages must not depend on Reified. Only the explicit HTTP host adapters may reference Reified packages.
- Model application and operational dependencies explicitly in `'env`; keep the ambient runtime for executor mechanics only.
- Use `BindError` only at a `flow { }` bind site when a source error must be assigned or mapped immediately before binding.
- Prefer AOT- and trimming-safe designs. Do not introduce runtime reflection as the foundation for workflow or service-access APIs.

## Dev Doc Organization

- Keep active architecture in `dev-docs/PLAN.md`, active work in `dev-docs/TASKS.md`, and high-level durable
  decisions in `dev-docs/decisions/README.md`.
- Keep completed work out of `dev-docs/TASKS.md`; keep the remaining active queue there for loop scripts.
- Keep speculative or pre-idea work in `dev-docs/current-ideas/`.
- Do not retain detailed historical specs after their useful decisions have been folded into current instructions. Delete stale specs instead of archiving large files that no longer match the codebase.

## API Naming

- Environment access is `Flow.env` (the whole record) and `Flow.envWith f` (one value out of it); `Layer.envWith`
  mirrors it. These correspond to ZIO's `ZIO.environment` and `ZIO.environmentWith`. Name the combinator after the
  `'env` type parameter it hands you, so the rule stays derivable from any signature.
- A package's `service` accessor — `Clock.service`, `Http.service` — is the type-directed form and corresponds to
  ZIO's `ZIO.service[A]`. Keep `service` meaning only that.
- There is no `Flow.service`. `'env` is an ordinary F# record, so a service is selected with a projection
  (`Flow.envWith _.Clock`), not looked up by type or tag. `ServiceProvider.get` remains the host-boundary escape
  hatch for dynamic container lookup.
- Reserve `read` for I/O that actually reads something (`Console.read`, stream readers). It must not name a pure
  environment projection.

## Writing

- Write concrete prose that names the API, behavior, tradeoff, or decision directly. Remove generic AI filler,
  promotional adjectives, grandiose claims, repetitive summaries, fake quotations, and throat-clearing such as
  "In today's landscape", "It's important to note", "powerful", "robust", "seamless", and "comprehensive" when the
  sentence does not prove a specific claim. Do not use slogans such as "not just X, but Y" in place of an explanation.
- In documentation and code comments, explain facts a reader cannot already see from the signature or implementation.
  Prefer a short example or a precise constraint over restating the member name in prose.

## Test Authoring

- Tests that demonstrate public APIs should use the expected end-user pipeline form, not a lower-level or transitional shape, unless the test is explicitly covering that lower-level API. Public API tests are examples readers copy from; keep their formatting aligned with the authoring style the library intends to teach.
- Do not define shared fixtures as module-level `let` values in xUnit test modules. Build fixtures inside each test or expose them as functions.

## Doc Workflow

- Treat `output/**`, `.livedocs/build-history/**`, and API reference pages as generated outputs. Root `llms.txt` and `docs/llms.txt` are hand-written product entry points.
- When changing an API, update source comments and any `docs/api/{EntityId}.md` enrichment page, then rebuild with FsLiveDocs. Do not commit generated reference pages.
- Use numbered task folders under `docs/`; FsLiveDocs strips ordering prefixes from generated URLs.
- For small checkbox tasks, defer `bash scripts/validate-docs.sh` until the phase end or a release/deploy checkpoint. `dev-docs/**` idea/planning notes do not require validation.

## Versioning and Compatibility

- **Before 1.0:** Bravely iterate. Remove old APIs and "old ways" immediately when a better alternative is established. Do not maintain compatibility aliases or stale patterns.
- **Post 1.0:** Standard semantic versioning applies. Maintain compatibility and use deprecation cycles for breaking changes.
- Packable projects inherit the shared version from `Directory.Build.props`; do not declare project-specific `<Version>` values.
- A release tag such as `v0.7.0` produces all public Axial NuGet packages at version `0.7.0`.
- Revisit independent package versioning after the package boundaries stabilize, likely at or after 1.0.

## Documentation Integrity

- **Validate At Phase Or Release Boundaries:** For small checkbox tasks, defer `bash scripts/validate-docs.sh` until phase end or a release/deploy checkpoint, even after changes to user-facing docs, public API signatures, XML comments, examples, reference enrichment, `llms.txt`, or site content. `dev-docs/**` idea/planning notes and code-only changes with no public-doc impact do not require validation. Use `bash scripts/preview-docs.sh` only when a live server is needed for browser review or screenshots.
- **Preview Lifecycle:** `bash scripts/preview-docs.sh` stops cleanly on `SIGHUP`, `TERM`, or `INT`. It can also be stopped by creating `$AXIAL_DOCS_PREVIEW_STOP_FILE`, which defaults to `/tmp/axial-docs-preview.stop`.
- **Link Integrity:** Ensure that all cross-references between guides and reference pages are valid. Broken links degrade the experience for both humans and AI agents.
- **Code Highlighting:** Ensure all code examples are wrapped in triple-backticks with the `fsharp` language hint for proper syntax highlighting.
