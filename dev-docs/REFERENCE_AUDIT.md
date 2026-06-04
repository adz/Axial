# Reference Audit

Snapshot after the current docgen cleanup pass.

## Fixed in this pass

- Legacy `https://adz.github.io/FsFlow/reference/FsFlow/*.html` links are no longer emitted into `docs/reference/**`.
- `Flow` reference pages are grouped into sub-sections:
  - `construction`
  - `environment`
  - `composition`
  - `execution`
  - `resources`
  - `concurrency`
  - `scheduling`
- Service `*.layer` entries now resolve to the actual service layer values instead of the generic `layer { }` builder page.
- `Scope` remains present and linked in the reference tree.

## Remaining reference gaps

These are still worth fixing with dedicated pages instead of section-level aliases.

### Missing dedicated member pages

- `Check.not`
- `Check.and`
- `Check.or`

The XML doc ids for these reserved-word members currently do not match the existing page-spec lookup logic cleanly.

### Still aliased to a broader page instead of a dedicated target

- `Never`
- `LogLevel`
- `RetryPolicy<'error>`
- `StmBuilder`
- `Path`

These no longer leave broken links in the generated markdown, but they should eventually have first-class reference pages or a clearer home.

## Suggested next cleanup

1. Teach `scripts/docgen/Program.fs` to resolve XML ids for reserved-word members such as `Check.and`.
2. Decide whether `Never`, `LogLevel`, and `RetryPolicy<'error>` belong in their own top-level reference section or in an existing section.
3. Add dedicated builder/type pages where aliases are still carrying the load.
