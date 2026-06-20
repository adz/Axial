# API Reference Page Shape

## Extracted From

- `dev-docs/TASKS.md`
- `dev-docs/PLAN.md`

## Source Date

- 2026-05-03: docs audit for per-API reference coverage

## Decision

Axial documents every public API surface with its own reference page, following a "one page per function" model similar to FsToolkit.ErrorHandling.

The reference shape is:

- **Surface Pages:** One page per public type, module, or builder that serves as a categorized index.
- **Function Pages:** One dedicated page per public function or member, nested under its parent surface.
- **Side Menu:** Reflects this granularity, allowing direct navigation to individual functions.
- **Rich Content:** Every function page includes:
    - A clear title and description.
    - Full summary and remarks lifted from XML doc comments.
    - Multiple runnable-style examples lifted from XML `<example>` tags.
    - Explicit namespace, module, and signature information.
    - A direct source link to the implementation.

Documentation rules:

- **Source-driven:** Lift all reference content from XML doc comments (`<summary>`, `<remarks>`, `<example>`) to ensure synchronization between code and docs.
- **Function-level Granularity:** Every member in a `module` or `type` that is listed in the `pageSpecs` gets its own markdown file.
- **Zero legacy:** Maintain no legacy terminology or compatibility aliases in the new structure.

Versioning rules:

- released docs link source pages to the matching release snapshot.
- unreleased docs link source pages to `main`.

## Why

- A page-per-surface layout makes the reference easier to scan.
- Side-menu visibility prevents important APIs from being buried behind a package hub.
- Versioned source links make the docs auditable against the exact code release.
- `main` links for next docs keep unreleased pages useful without pretending they are frozen.

## Consequences

- The current reference pages should be split until every public surface is directly reachable.
- Package hubs should stay short and delegate to the per-surface pages.
- The docs generation workflow must preserve versioned and next-branch source links.
- Public docs should not rely on narrative pages to stand in for missing API pages.
